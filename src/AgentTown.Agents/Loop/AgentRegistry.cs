using System.Collections.Concurrent;
using AgentTown.Agents.State;

namespace AgentTown.Agents.Loop;

/// <summary>
/// All agents known to this service, by id. Loading from the database (and seeding from
/// <c>seed/agents.json</c> when it is empty) is added once Persistence exists.
/// </summary>
public sealed class AgentRegistry
{
    private readonly ConcurrentDictionary<Guid, AgentState> _agents = new();

    public int Count => _agents.Count;

    public IReadOnlyCollection<AgentState> All => _agents.Values.OrderBy(a => a.Name, StringComparer.Ordinal).ToArray();

    public void Add(AgentState agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        if (!_agents.TryAdd(agent.Id, agent))
            throw new InvalidOperationException($"Agent {agent.Id} ({agent.Name}) is already registered.");
    }

    public AgentState? Find(Guid agentId) => _agents.GetValueOrDefault(agentId);

    /// <summary>Other agents in the same building (people the agent can talk to).</summary>
    public IReadOnlyList<AgentState> NearbyOf(AgentState agent) =>
        agent.LocationId is null
            ? []
            : _agents.Values.Where(other => other.Id != agent.Id && other.IsAt(agent.LocationId)).ToArray();
}
