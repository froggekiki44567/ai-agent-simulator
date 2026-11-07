namespace AgentTown.Contracts.Mind;

/// <summary>One memory of an agent, as shown in the agent panel.</summary>
/// <param name="Kind">Observation, Conversation, Reflection or Event.</param>
/// <param name="Importance">1 (forgettable) to 10 (life-changing).</param>
public sealed record MemoryView(Guid Id, long Tick, string Kind, string Text, int Importance);

/// <summary>How an agent sees another agent, as shown in the agent panel.</summary>
/// <param name="Affinity">-100 (enemy) … 100 (close friend).</param>
/// <param name="Label">Short description, e.g. "a good friend who owes you 20.00 EUR".</param>
/// <param name="Balance">Positive = the other owes this agent money, negative = this agent owes the other.</param>
public sealed record RelationshipView(
    Guid OtherAgentId,
    string OtherName,
    int Affinity,
    string Label,
    decimal Balance,
    long LastInteractionTick);

/// <summary>
/// Read-only view of an agent's "mind" (memories, relationships) for the Api
/// (<c>GET /api/agents/{id}/memories</c>, <c>GET /api/agents/{id}/relationships</c>).
/// Implemented by the Agents side; until the Memories/Relationships tables exist it reads the in-memory stores.
/// </summary>
public interface IAgentMindQuery
{
    /// <summary>The newest memories first, at most <paramref name="limit"/> (1–100).</summary>
    Task<IReadOnlyList<MemoryView>> GetMemoriesAsync(Guid agentId, int limit = 10, CancellationToken cancellationToken = default);

    /// <summary>Everyone the agent knows, the closest first.</summary>
    Task<IReadOnlyList<RelationshipView>> GetRelationshipsAsync(Guid agentId, CancellationToken cancellationToken = default);
}
