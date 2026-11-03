namespace ThorntonEnergy.Chat.Api;

public sealed record ChatRequest(
    List<ChatInputMessage> Messages,
    OutageConfirmation? OutageConfirmation = null);

public sealed record ChatInputMessage(string Role, string Content);

public sealed record OutageConfirmation(bool Confirmed, string Address, string Description);

public sealed record OutageConfirmationPrompt(string Address, string Description);

public sealed record AgentToolCall(string Id, string Name, string Arguments);

public sealed record AgentTurn(List<AgentToolCall> ToolCalls);

public sealed record ToolExecutionResult(string Content, OutageConfirmationPrompt? ConfirmationRequired = null);

public sealed record AgentMessage(string Role, string Content)
{
    public static AgentMessage ForToolCalls(IReadOnlyCollection<AgentToolCall> calls) =>
        new("assistant", System.Text.Json.JsonSerializer.Serialize(new
        {
            tool_calls = calls.Select(c => new
            {
                id = c.Id,
                type = "function",
                function = new { name = c.Name, arguments = c.Arguments }
            })
        }));

    public static AgentMessage ToolResult(string id, string content) =>
        new("tool", System.Text.Json.JsonSerializer.Serialize(new { tool_call_id = id, content }));
}

public sealed class FoundryOptions
{
    public const string SectionName = "Foundry";
    public string Endpoint { get; set; } = "";
    public string AgentId { get; set; } = "";
    public string ApiKey { get; set; } = "";
}

public sealed class FoundryConfigurationException : Exception { }
