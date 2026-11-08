namespace AgentTown.Agents.Memory;

/// <summary>
/// Where memories live. <see cref="InMemoryMemoryStore"/> is used until the <c>Memories</c> table
/// (pgvector, Persistence ⇄ B) exists; then a database store implements the same interface.
/// </summary>
public interface IMemoryStore
{
    Task AddAsync(MemoryRecord memory, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryRecord>> GetByAgentAsync(Guid agentId, CancellationToken cancellationToken = default);

    Task MarkAccessedAsync(IEnumerable<Guid> memoryIds, long tick, CancellationToken cancellationToken = default);
}
