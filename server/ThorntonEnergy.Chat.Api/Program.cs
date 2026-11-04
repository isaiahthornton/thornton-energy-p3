using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using ThorntonEnergy.Chat.Api;
using ThorntonEnergy.Chat.Api.Services;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Extensions;

const string ChatRateLimit = "chat";
var EventJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);

var builder = WebApplication.CreateBuilder(args);
builder.AddJWTAuth();
builder.Services.Configure<FoundryOptions>(builder.Configuration.GetSection(FoundryOptions.SectionName));
builder.Services.AddHttpClient<IFoundryAgentClient, FoundryAgentClient>(client =>
    client.Timeout = TimeSpan.FromMinutes(3))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient<CustomerToolExecutor>(client =>
    client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient<CustomerSnapshotService>(client =>
    client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// Every message can cost money.
//  - Each signed-in customer gets 15 chat requests a minute.
//  - Visitors share one pool of 20 a minute (we cannot tell visitors apart behind the load balancer),
//    so a script cannot run up the bill. When the pool is used up, visitors get a "try again in a minute" message.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(ChatRateLimit, httpContext =>
    {
        var customerId = httpContext.User.IsInRole(AppRoles.Customer)
            ? httpContext.User.FindFirstValue(AppClaims.CustomerId)
            : null;
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: customerId is null ? "visitors" : "customer-" + customerId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = customerId is null ? 20 : 15,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    });
    options.OnRejected = async (context, cancellationToken) =>
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Lots of people are chatting right now. Please wait a minute and try again." },
            cancellationToken);
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).AllowAnonymous();
app.MapGet("/health/ready", (IOptions<FoundryOptions> options) =>
{
    var config = options.Value;
    return Uri.TryCreate(config.Endpoint, UriKind.Absolute, out var endpoint)
        && endpoint.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrWhiteSpace(config.AgentId)
        ? Results.Ok(new { status = "ready" })
        : Results.Problem("Foundry Endpoint and AgentId must be configured.", statusCode: 503);
}).AllowAnonymous();

// Two kinds of caller:
//  - a signed-in customer (role "Customer" in the login token): gets the personal assistant
//  - everyone else (visitors, and staff): gets the general assistant, with no account access at all
// The role comes from the signed token, never from anything the browser or the model sends.
static bool TryGetCustomer(HttpContext context, out int customerId, out string token)
{
    customerId = 0;
    token = "";
    if (!context.User.IsInRole(AppRoles.Customer))
        return false;
    var authorization = context.Request.Headers.Authorization.ToString();
    if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        token = authorization["Bearer ".Length..].Trim();
    return int.TryParse(context.User.FindFirstValue(AppClaims.CustomerId), out customerId)
        && customerId > 0 && token.Length > 0;
}

// Used by the chat window to say "Hi <first name>" when it opens (no AI call, so it is free)
app.MapGet("/api/chat/hello", async (HttpContext context, CustomerSnapshotService snapshots) =>
{
    if (!TryGetCustomer(context, out var customerId, out var token))
        return Results.Ok(new { firstName = (string?)null });
    var firstName = await snapshots.GetFirstNameAsync(customerId, token, context.RequestAborted);
    return Results.Ok(new { firstName });
})
.AllowAnonymous()
.RequireRateLimiting(ChatRateLimit);

app.MapPost("/api/chat/stream", async (
    ChatRequest request,
    HttpContext context,
    IFoundryAgentClient foundry,
    CustomerToolExecutor tools,
    CustomerSnapshotService snapshots,
    IOptions<FoundryOptions> foundryOptions) =>
{
    var isCustomer = TryGetCustomer(context, out var customerId, out var customerToken);
    // A customer role without a usable customer id is a broken token: refuse rather than quietly downgrade
    if (context.User.IsInRole(AppRoles.Customer) && !isCustomer)
        return Results.Forbid();

    // Visitors get shorter conversations and shorter messages (they cost money and are anonymous)
    var maxMessages = isCustomer ? 20 : 10;
    var maxLength = isCustomer ? 2000 : 500;
    if (request.Messages is null || request.Messages.Count == 0 || request.Messages.Count > maxMessages
        || request.Messages.Any(m => m is null || (m.Role != "user" && m.Role != "assistant")
            || string.IsNullOrWhiteSpace(m.Content) || m.Content.Length > maxLength))
        return Results.BadRequest(new { message = $"A chat must contain 1–{maxMessages} user/assistant messages, each up to {maxLength:N0} characters." });

    context.Response.StatusCode = StatusCodes.Status200OK;
    context.Response.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache, no-store";
    context.Response.Headers.Append("X-Accel-Buffering", "no");

    // camelCase, because the browser reads "address" and "description" (the default would send "Address")
    async Task SendEvent(string name, object data)
    {
        await context.Response.WriteAsync($"event: {name}\n");
        await context.Response.WriteAsync($"data: {JsonSerializer.Serialize(data, EventJson)}\n\n");
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }

    try
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        string systemPrompt;
        if (isCustomer)
        {
            // What we know about this customer today goes into the assistant's instructions
            var snapshot = await snapshots.BuildAsync(customerId, customerToken, context.RequestAborted);
            systemPrompt = CustomerPrompt.Build(snapshot.Text, today);
        }
        else
        {
            systemPrompt = VisitorPrompt.Build(today);
        }

        var messages = new List<AgentMessage> { new("system", systemPrompt) };
        messages.AddRange(request.Messages.Select(m => new AgentMessage(m.Role, m.Content)));
        var allowedTools = isCustomer ? ToolDefinitions.All : ToolDefinitions.Visitor;

        // A confirmation is good for one outage report only, so a repeated model call cannot file it twice
        var confirmation = isCustomer ? request.OutageConfirmation : null;
        var outageFiled = false;

        // The customer tapped "Yes, send it" on the card. File exactly what the card showed right now,
        // instead of hoping the model repeats the same words, then let the model tell the customer.
        if (confirmation is { Confirmed: true })
        {
            var confirmedCall = new AgentToolCall("confirmed-outage", "report_outage",
                JsonSerializer.Serialize(new { address = confirmation.Address, description = confirmation.Description }));
            var filed = await tools.ExecuteAsync(confirmedCall, customerId, customerToken, confirmation, context.RequestAborted);
            messages.Add(AgentMessage.ForToolCalls([confirmedCall]));
            messages.Add(AgentMessage.ToolResult(confirmedCall.Id, filed.Content));
            outageFiled = true;
            confirmation = null;
        }

        for (var round = 0; round < 4; round++)
        {
            var turn = await foundry.StreamTurnAsync(
                foundryOptions.Value,
                messages,
                async delta => await SendEvent("delta", new { text = delta }),
                allowedTools,
                context.RequestAborted);

            if (turn.ToolCalls.Count == 0)
                break;

            messages.Add(AgentMessage.ForToolCalls(turn.ToolCalls));
            var pauseForConfirmation = false;
            foreach (var call in turn.ToolCalls)
            {
                // Visitors may only read the public plans, whatever the model asks for
                var result = !isCustomer && call.Name != "get_rate_plans"
                    ? new ToolExecutionResult("{\"error\":\"Please sign in to use this.\"}")
                    : outageFiled && call.Name == "report_outage"
                        ? new ToolExecutionResult("{\"error\":\"The report was already filed. Do not file another.\"}")
                        : await tools.ExecuteAsync(call, customerId, customerToken, confirmation, context.RequestAborted);

                if (result.ConfirmationRequired is not null)
                {
                    await SendEvent("confirmation_required", result.ConfirmationRequired);
                    pauseForConfirmation = true;
                    break;
                }

                if (call.Name == "report_outage")
                    confirmation = null;

                messages.Add(AgentMessage.ToolResult(call.Id, result.Content));
            }

            if (pauseForConfirmation)
                return Results.Empty;
        }

        await SendEvent("done", new { });
        return Results.Empty;
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
        return Results.Empty;
    }
    catch (FoundryConfigurationException)
    {
        if (!context.Response.HasStarted)
            return Results.Problem("Chat provider is not configured. Contact support.", statusCode: 503);
        await SendEvent("error", new { message = "Chat is not configured yet. Please try again later." });
        return Results.Empty;
    }
    catch (Exception)
    {
        // Do not log request content, customer tokens, provider headers, or provider response bodies.
        if (!context.Response.HasStarted)
            return Results.Problem("Chat is temporarily unavailable.", statusCode: 502);
        await SendEvent("error", new { message = "Chat is temporarily unavailable. Please retry." });
        return Results.Empty;
    }
})
.AllowAnonymous()
.RequireRateLimiting(ChatRateLimit)
.DisableAntiforgery();

app.Run();

public partial class Program;
