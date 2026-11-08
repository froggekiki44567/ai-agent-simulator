using AgentTown.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace AgentTown.Agents.Loop;

/// <summary>Temporary publisher that only writes to the log. Replaced by the NATS publisher.</summary>
public sealed class LoggingAgentEventPublisher(ILogger<LoggingAgentEventPublisher> logger) : IAgentEventPublisher
{
    public Task PublishActionAsync(ActionRequested action, long tick, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Tick {Tick} | {AgentId} requests {Action} {Target} ({Source}): {Reason}",
            tick, action.AgentId, action.Type, action.TargetId, action.Source, action.Reason);
        return Task.CompletedTask;
    }

    public Task PublishThoughtAsync(AgentThought thought, long tick, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Tick {Tick} | {AgentId} thinks: {Thought}", tick, thought.AgentId, thought.Text);
        return Task.CompletedTask;
    }

    public Task PublishConversationAsync(ConversationMessage message, long tick, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Tick {Tick} | {SpeakerId} says to {ListenerId}: {Text}", tick, message.SpeakerId, message.ListenerId, message.Text);
        return Task.CompletedTask;
    }
}
