namespace AgentTown.Agents.Relationships;

/// <summary>
/// Where relationships live. <see cref="InMemoryRelationshipStore"/> is used until the <c>Relationships</c>
/// table exists in Persistence (⇄ B); then a database store implements the same interface.
/// </summary>
public interface IRelationshipStore
{
    Task<RelationshipRecord?> GetAsync(Guid agentId, Guid otherAgentId, CancellationToken cancellationToken = default);

    /// <summary>Everyone this agent knows.</summary>
    Task<IReadOnlyList<RelationshipRecord>> GetByAgentAsync(Guid agentId, CancellationToken cancellationToken = default);

    Task UpsertAsync(RelationshipRecord relationship, CancellationToken cancellationToken = default);
}
