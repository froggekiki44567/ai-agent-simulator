namespace AgentTown.Agents.Relationships;

/// <summary>
/// How one agent sees another. Matches the planned <c>Relationships</c> table
/// (AgentId, OtherAgentId, Affinity -100…100, LastInteractionTick) plus <see cref="Balance"/>.
/// Relationships are directed: Jonas → Petras can differ from Petras → Jonas.
/// </summary>
/// <param name="Affinity">-100 (enemy) … 100 (close friend). 0 = just met.</param>
/// <param name="Balance">Money between them from transfers: positive = the other owes this agent, negative = this agent owes the other.</param>
public sealed record RelationshipRecord(
    Guid AgentId,
    Guid OtherAgentId,
    int Affinity,
    long LastInteractionTick,
    decimal Balance = 0)
{
    public const int MinAffinity = -100;
    public const int MaxAffinity = 100;
}
