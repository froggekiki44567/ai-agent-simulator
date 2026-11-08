using AgentTown.Agents.Memory;
using AgentTown.Agents.Relationships;
using AgentTown.Contracts.Mind;

namespace AgentTown.Agents.Mind;

/// <summary>
/// <see cref="IAgentMindQuery"/> over the Agents service's own stores. Names of other agents come from the agent registry.
/// Reading memories here does not count as "accessing" them, so the UI never changes what an agent recalls.
/// </summary>
public sealed class AgentMindQuery(IMemoryStore memories, RelationshipService relationships) : IAgentMindQuery
{
    public const int MaxLimit = 100;

    public async Task<IReadOnlyList<MemoryView>> GetMemoriesAsync(Guid agentId, int limit = 10, CancellationToken cancellationToken = default)
    {
        var all = await memories.GetByAgentAsync(agentId, cancellationToken);
        return all
            .OrderByDescending(m => m.Tick)
            .ThenByDescending(m => m.Importance)
            .Take(Math.Clamp(limit, 1, MaxLimit))
            .Select(m => new MemoryView(m.Id, m.Tick, m.Kind.ToString(), m.Text, m.Importance))
            .ToArray();
    }

    public async Task<IReadOnlyList<RelationshipView>> GetRelationshipsAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        var all = await relationships.GetForAgentAsync(agentId, cancellationToken);
        return all
            .OrderByDescending(r => r.Affinity)
            .ThenByDescending(r => r.LastInteractionTick)
            .Select(r => new RelationshipView(
                r.OtherAgentId,
                relationships.NameOf(r.OtherAgentId) ?? "unknown",
                r.Affinity,
                relationships.Describe(r),
                r.Balance,
                r.LastInteractionTick))
            .ToArray();
    }
}
