namespace AgentTown.Contracts.Events;

/// <summary>Api → World: the user injected an event (e.g. "BankCollapse").</summary>
/// <param name="Type">Preset event type, or "Custom" for free text.</param>
/// <param name="Parameters">Event-specific parameters (e.g. "multiplier" = "2").</param>
/// <param name="Description">Human-readable description (free text for custom events).</param>
public sealed record WorldEventInjected(
    Guid EventId,
    string Type,
    IReadOnlyDictionary<string, string> Parameters,
    string Description);

/// <summary>World → Agents, Api: the event's effect has been applied to the world.</summary>
public sealed record WorldEventApplied(
    Guid EventId,
    string Type,
    string Description,
    IReadOnlyList<Guid> AffectedAgentIds,
    IReadOnlyList<string> AffectedBuildingIds,
    long AppliedAtTick);
