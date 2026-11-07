using System.Collections.Concurrent;

namespace AgentTown.Agents.Memory;

/// <summary>Thread-safe in-process store. Keeps at most <see cref="MemoryOptions.MaxMemoriesPerAgent"/> per agent.</summary>
public sealed class InMemoryMemoryStore(MemoryOptions options) : IMemoryStore
{
    private readonly ConcurrentDictionary<Guid, List<MemoryRecord>> _byAgent = new();
    private readonly ConcurrentDictionary<Guid, MemoryRecord> _byId = new();

    public Task AddAsync(MemoryRecord memory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memory);
        var list = _byAgent.GetOrAdd(memory.AgentId, _ => []);
        lock (list)
        {
            list.Add(memory);
            _byId[memory.Id] = memory;

            var max = Math.Max(1, options.MaxMemoriesPerAgent);
            while (list.Count > max)
            {
                // Forget the least important memory; among equals, the oldest.
                var forget = list.OrderBy(m => m.Importance).ThenBy(m => m.Tick).First();
                list.Remove(forget);
                _byId.TryRemove(forget.Id, out _);
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MemoryRecord>> GetByAgentAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        if (!_byAgent.TryGetValue(agentId, out var list))
            return Task.FromResult<IReadOnlyList<MemoryRecord>>([]);

        lock (list)
            return Task.FromResult<IReadOnlyList<MemoryRecord>>(list.ToArray());
    }

    public Task MarkAccessedAsync(IEnumerable<Guid> memoryIds, long tick, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memoryIds);
        foreach (var id in memoryIds)
        {
            if (_byId.TryGetValue(id, out var memory))
                memory.LastAccessedTick = Math.Max(memory.LastAccessedTick, tick);
        }

        return Task.CompletedTask;
    }
}
