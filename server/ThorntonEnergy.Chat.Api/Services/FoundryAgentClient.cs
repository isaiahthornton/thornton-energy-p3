using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ThorntonEnergy.Chat.Api;

namespace ThorntonEnergy.Chat.Api.Services;

/// <summary>
/// OpenAI-compatible streaming adapter boundary for a Foundry-agent gateway.
/// The project/agent values and complete endpoint are deployment configuration;
/// no Azure resource identifier or route is assumed here.
/// </summary>
public sealed class FoundryAgentClient(HttpClient httpClient, ILogger<FoundryAgentClient> logger) : IFoundryAgentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AgentTurn> StreamTurnAsync(
        FoundryOptions options,
        IReadOnlyList<AgentMessage> messages,
        Func<string, Task> onTextDelta,
        IReadOnlyList<object> tools,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(options.AgentId))
            throw new FoundryConfigurationException();

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            request.Headers.TryAddWithoutValidation("api-key", options.ApiKey);

        request.Content = JsonContent.Create(new
        {
            model = options.AgentId,
            stream = true,
            messages = messages.Select(ToWireMessage),
            tools,
            tool_choice = "auto",
            temperature = 0.3,            // steady, factual answers
            max_completion_tokens = 500   // keeps replies short and cost small
        }, options: JsonOptions);

        using var response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Deliberately avoid reading/logging response body, which can contain sensitive provider details.
            logger.LogWarning("Configured chat provider returned HTTP {StatusCode}.", (int)response.StatusCode);
            throw new HttpRequestException("Chat provider request failed.");
        }

        var toolCalls = new SortedDictionary<int, MutableToolCall>();
        await foreach (var data in ReadSseDataAsync(response, cancellationToken))
        {
            if (data == "[DONE]")
                break;
            using var document = JsonDocument.Parse(data);
            if (!document.RootElement.TryGetProperty("choices", out var choices)
                || choices.GetArrayLength() == 0
                || !choices[0].TryGetProperty("delta", out var delta))
                continue;

            if (delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
            {
                var text = content.GetString();
                if (!string.IsNullOrEmpty(text))
                    await onTextDelta(text);
            }

            if (!delta.TryGetProperty("tool_calls", out var calls) || calls.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in calls.EnumerateArray())
            {
                var index = item.TryGetProperty("index", out var indexElement) ? indexElement.GetInt32() : 0;
                if (!toolCalls.TryGetValue(index, out var call))
                    toolCalls[index] = call = new MutableToolCall();
                if (item.TryGetProperty("id", out var id))
                    call.Id = id.GetString() ?? call.Id;
                if (!item.TryGetProperty("function", out var function))
                    continue;
                if (function.TryGetProperty("name", out var name))
                    call.Name += name.GetString();
                if (function.TryGetProperty("arguments", out var arguments))
                    call.Arguments += arguments.GetString();
            }
        }

        return new AgentTurn(toolCalls.Values
            .Where(c => !string.IsNullOrWhiteSpace(c.Id) && !string.IsNullOrWhiteSpace(c.Name))
            .Select(c => new AgentToolCall(c.Id, c.Name, string.IsNullOrWhiteSpace(c.Arguments) ? "{}" : c.Arguments))
            .ToList());
    }

    private static object ToWireMessage(AgentMessage message)
    {
        if (message.Role == "assistant" && message.Content.StartsWith("{\"tool_calls\"", StringComparison.Ordinal))
            return new { role = "assistant", tool_calls = ExtractToolCalls(message.Content) };
        if (message.Role == "tool")
        {
            using var parsed = JsonDocument.Parse(message.Content);
            return new
            {
                role = "tool",
                tool_call_id = parsed.RootElement.GetProperty("tool_call_id").GetString(),
                content = parsed.RootElement.GetProperty("content").GetString()
            };
        }
        return new { role = message.Role, content = message.Content };
    }

    private static object ExtractToolCalls(string content)
    {
        using var parsed = JsonDocument.Parse(content);
        return parsed.RootElement.GetProperty("tool_calls").Clone();
    }

    private static async IAsyncEnumerable<string> ReadSseDataAsync(
        HttpResponseMessage response,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var data = new StringBuilder();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                if (data.Length > 0)
                {
                    yield return data.ToString();
                    data.Clear();
                }
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (data.Length > 0)
                    data.Append('\n');
                data.Append(line.AsSpan(5).TrimStart());
            }
        }
        if (data.Length > 0)
            yield return data.ToString();
    }

    private sealed class MutableToolCall
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Arguments { get; set; } = "";
    }
}

internal static class ToolDefinitions
{
    private static readonly object RatePlansTool =
        Tool("get_rate_plans", "Read the available rate plans (price per kWh, monthly fee, description). Public information.", new { type = "object", properties = new { }, additionalProperties = false });

    // What a signed-in customer's assistant may use
    public static readonly object[] All =
    [
        Tool("get_profile", "Read the authenticated customer's profile.", new { type = "object", properties = new { }, additionalProperties = false }),
        Tool("get_meters", "Read meters belonging to the authenticated customer.", new { type = "object", properties = new { }, additionalProperties = false }),
        Tool("get_meter_readings", "Read meter readings belonging to the authenticated customer.", new { type = "object", properties = new { }, additionalProperties = false }),
        Tool("get_bills", "Read bills belonging to the authenticated customer. This does not initiate payment.", new { type = "object", properties = new { }, additionalProperties = false }),
        RatePlansTool,
        Tool("get_outages", "Read outage reports belonging to the authenticated customer.", new { type = "object", properties = new { }, additionalProperties = false }),
        Tool("report_outage", "Create an outage report only after the customer explicitly confirms the exact address and description.", new
        {
            type = "object",
            properties = new
            {
                address = new { type = "string" },
                description = new { type = "string" }
            },
            required = new[] { "address", "description" },
            additionalProperties = false
        })
    ];

    // What the visitor assistant may use: public information only
    public static readonly object[] Visitor = [RatePlansTool];

    private static object Tool(string name, string description, object parameters) => new
    {
        type = "function",
        function = new { name, description, parameters }
    };
}
