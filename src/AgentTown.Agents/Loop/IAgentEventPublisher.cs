using AgentTown.Contracts.Events;

namespace AgentTown.Agents.Loop;

/// <summary>
/// Everything the Agents service sends out. The NATS implementation (on top of B's <c>INatsBus</c>)
/// publishes to <c>Subjects.ActionsRequested</c>, <c>Subjects.AgentThought</c> and <c>Subjects.ConversationMessage</c>.
/// Until Messaging exists, <see cref="LoggingAgentEventPublisher"/> just logs.
/// </summary>
public interface IAgentEventPublisher
{
    Task PublishActionAsync(ActionRequested action, long tick, CancellationToken cancellationToken = default);
    Task PublishThoughtAsync(AgentThought thought, long tick, CancellationToken cancellationToken = default);
    Task PublishConversationAsync(ConversationMessage message, long tick, CancellationToken cancellationToken = default);
}
