using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ThorntonEnergy.Chat.Api;

namespace ThorntonEnergy.Chat.Api.Services;

public sealed class CustomerToolExecutor(HttpClient httpClient, IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxResultCharacters = 12_000;

    public async Task<ToolExecutionResult> ExecuteAsync(
        AgentToolCall call,
        int customerId,
        string customerToken,
        OutageConfirmation? confirmation,
        CancellationToken cancellationToken)
    {
        string? service = null;
        string? path = null;
        HttpMethod method = HttpMethod.Get;
        string? body = null;

        switch (call.Name)
        {
            case "get_profile":
                service = "Customers";
                path = $"api/customers/{customerId}";
                break;
            case "get_meters":
                service = "Metering";
                path = "api/meters";
                break;
            case "get_meter_readings":
                service = "Metering";
                path = "api/meterReadings";
                break;
            case "get_bills":
                service = "Billing";
                path = "api/bills";
                break;
            case "get_rate_plans":
                service = "Customers";
                path = "api/ratePlans";
                break;
            case "get_outages":
                service = "Outages";
                path = "api/outages";
                break;
            case "report_outage":
                if (!TryParseOutage(call.Arguments, out var address, out var description))
                    return new ToolExecutionResult("{\"error\":\"Invalid outage details.\"}");
                if (confirmation?.Confirmed != true
                    || !SameText(confirmation.Address, address)
                    || !SameText(confirmation.Description, description))
                    return new ToolExecutionResult("", new OutageConfirmationPrompt(address, description));

                service = "Outages";
                path = "api/outages";
                method = HttpMethod.Post;
                body = JsonSerializer.Serialize(new
                {
                    customerId = (int?)null,
                    address,
                    description
                }, JsonOptions);
                break;
            default:
                // Deny-by-default: no admin endpoints, payment operations, or meter-reading writes.
                return new ToolExecutionResult("{\"error\":\"This customer action is not available.\"}");
        }

        var baseUrl = configuration[$"Services:{service}"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            return new ToolExecutionResult("{\"error\":\"Customer service is unavailable.\"}");

        using var request = new HttpRequestMessage(method, new Uri(baseUri, path));
        if (!string.IsNullOrEmpty(customerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new ToolExecutionResult("{\"error\":\"The requested account operation could not be completed.\"}");

        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        // Keep what the model reads small: fewer tokens, lower cost, faster answers
        if (result.Length > MaxResultCharacters)
            result = result[..MaxResultCharacters] + " ...(list shortened)";
        return new ToolExecutionResult(result);
    }

    // The customer confirmed these exact words, so ignore harmless differences in spacing or capital letters
    private static bool SameText(string a, string b) =>
        string.Equals(string.Join(' ', a.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)),
                      string.Join(' ', b.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)),
                      StringComparison.OrdinalIgnoreCase);

    private static bool TryParseOutage(string json, out string address, out string description)
    {
        address = "";
        description = "";
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            address = root.GetProperty("address").GetString()?.Trim() ?? "";
            description = root.GetProperty("description").GetString()?.Trim() ?? "";
            return address.Length is > 0 and <= 200 && description.Length is > 0 and <= 1000;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
