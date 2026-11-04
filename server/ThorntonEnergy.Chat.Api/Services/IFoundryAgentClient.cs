using ThorntonEnergy.Chat.Api;

namespace ThorntonEnergy.Chat.Api.Services;

public interface IFoundryAgentClient
{
    Task<AgentTurn> StreamTurnAsync(
        FoundryOptions options,
        IReadOnlyList<AgentMessage> messages,
        Func<string, Task> onTextDelta,
        IReadOnlyList<object> tools,
        CancellationToken cancellationToken);
}
