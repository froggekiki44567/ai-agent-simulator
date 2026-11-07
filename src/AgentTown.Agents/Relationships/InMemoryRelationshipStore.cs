using System.Collections.Concurrent;

namespace AgentTown.Agents.Relationships;

/// <summary>Thread-safe in-process store.</summary>
public sealed class InMemoryRelationshipStore : IRelationshipStore
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, RelationshipRecord>> _byAgent = new();

    public Task<RelationshipRecord?> GetAsync(Guid agentId, Guid otherAgentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byAgent.TryGetValue(agentId, out var known) ? known.GetValueOrDefault(otherAgentId) : null);

    public Task<IReadOnlyList<RelationshipRecord>> GetByAgentAsync(Guid agentId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RelationshipRecord>>(
            _byAgent.TryGetValue(agentId, out var known) ? known.Values.ToArray() : []);

    public Task UpsertAsync(RelationshipRecord relationship, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        _byAgent.GetOrAdd(relationship.AgentId, _ => new())[relationship.OtherAgentId] = relationship;
        return Task.CompletedTask;
    }
}
