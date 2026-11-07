namespace AgentTown.Contracts.Model;

/// <summary>
/// Agent state after an action, as decided by World.
/// Every field is the <b>new absolute value</b>; <c>null</c> means "unchanged".
/// </summary>
public sealed record AgentStateDelta(
    decimal? Money = null,
    int? X = null,
    int? Y = null,
    string? LocationId = null,
    double? Hunger = null,
    double? Energy = null,
    double? Social = null,
    AgentStatus? Status = null)
{
    public static AgentStateDelta None { get; } = new();
}
