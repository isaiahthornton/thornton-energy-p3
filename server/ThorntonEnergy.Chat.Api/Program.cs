using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using ThorntonEnergy.Chat.Api;
using ThorntonEnergy.Chat.Api.Services;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddJWTAuth();
builder.Services.Configure<FoundryOptions>(builder.Configuration.GetSection(FoundryOptions.SectionName));
builder.Services.AddHttpClient<IFoundryAgentClient, FoundryAgentClient>(client =>
    client.Timeout = TimeSpan.FromMinutes(3))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient<CustomerToolExecutor>(client =>
    client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

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

app.MapPost("/api/chat/stream", async (
    ChatRequest request,
    HttpContext context,
    IFoundryAgentClient foundry,
    CustomerToolExecutor tools,
    IOptions<FoundryOptions> foundryOptions) =>
{
    if (request.Messages is null || request.Messages.Count == 0 || request.Messages.Count > 30
        || request.Messages.Any(m => m is null || (m.Role != "user" && m.Role != "assistant")
            || string.IsNullOrWhiteSpace(m.Content) || m.Content.Length > 8000))
        return Results.BadRequest(new { message = "A chat must contain 1–30 user/assistant messages, each up to 8,000 characters." });

    var customerId = context.User.FindFirstValue(AppClaims.CustomerId);
    if (!int.TryParse(customerId, out var parsedCustomerId) || parsedCustomerId <= 0)
        return Results.Forbid();

    var authorization = context.Request.Headers.Authorization.ToString();
    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        return Results.Unauthorized();
    var customerToken = authorization["Bearer ".Length..].Trim();

    context.Response.StatusCode = StatusCodes.Status200OK;
    context.Response.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache, no-store";
    context.Response.Headers.Append("X-Accel-Buffering", "no");

    async Task SendEvent(string name, object data)
    {
        await context.Response.WriteAsync($"event: {name}\n");
        await context.Response.WriteAsync($"data: {JsonSerializer.Serialize(data)}\n\n");
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }

    var messages = new List<AgentMessage>
    {
        new("system", CustomerPrompt.Text)
    };
    messages.AddRange(request.Messages.Select(m => new AgentMessage(m.Role, m.Content)));

    try
    {
        for (var round = 0; round < 4; round++)
        {
            var turn = await foundry.StreamTurnAsync(
                foundryOptions.Value,
                messages,
                async delta => await SendEvent("delta", new { text = delta }),
                context.RequestAborted);

            if (turn.ToolCalls.Count == 0)
                break;

            messages.Add(AgentMessage.ForToolCalls(turn.ToolCalls));
            var pauseForConfirmation = false;
            foreach (var call in turn.ToolCalls)
            {
                var result = await tools.ExecuteAsync(
                    call, parsedCustomerId, customerToken, request.OutageConfirmation,
                    context.RequestAborted);

                if (result.ConfirmationRequired is not null)
                {
                    await SendEvent("confirmation_required", result.ConfirmationRequired);
                    pauseForConfirmation = true;
                    break;
                }

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
.RequireAuthorization(policy => policy.RequireRole(AppRoles.Customer))
.DisableAntiforgery();

app.Run();

public partial class Program;
