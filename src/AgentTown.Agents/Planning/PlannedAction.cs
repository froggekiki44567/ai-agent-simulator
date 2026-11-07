using AgentTown.Contracts.Events;
using AgentTown.Contracts.Model;

namespace AgentTown.Agents.Planning;

/// <summary>What an agent wants to do next, before it is sent to World.</summary>
public sealed record PlannedAction(
    ActionType Type,
    string Reason,
    string? TargetId = null,
    string? ItemId = null,
    int Quantity = 0,
    decimal Amount = 0)
{
    public static PlannedAction Idle(string reason) => new(ActionType.Idle, reason);

    public ActionRequested ToRequest(Guid agentId, DecisionSource source, Guid? requestId = null) =>
        new(requestId ?? Guid.NewGuid(), agentId, Type, TargetId, ItemId, Quantity, Amount, Say: null, Reason, source);
}
