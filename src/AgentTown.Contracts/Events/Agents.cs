using AgentTown.Contracts.Model;

namespace AgentTown.Contracts.Events;

/// <summary>Agents → Api, Agents: one line of a conversation between two agents.</summary>
/// <param name="LineIndex">0-based position of this line in the conversation.</param>
/// <param name="IsFinal">True for the last line of the conversation.</param>
public sealed record ConversationMessage(
    Guid ConversationId,
    Guid SpeakerId,
    Guid ListenerId,
    string Text,
    int LineIndex,
    bool IsFinal);

/// <summary>Agents → Api: what an agent is "thinking" (shown in the UI).</summary>
public sealed record AgentThought(
    Guid AgentId,
    string Text,
    ActionType Action,
    DecisionSource Source);
