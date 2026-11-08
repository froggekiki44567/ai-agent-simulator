namespace AgentTown.Agents.Seed;

/// <summary>One agent as described in <c>seed/agents.json</c>.</summary>
/// <param name="Id">Fixed id, so the same agent keeps its id across restarts and in every service.</param>
/// <param name="Traits">3–5 personality traits used in the LLM prompt.</param>
/// <param name="JobId">Job id from the World's job list, or <c>null</c> for an unemployed agent.</param>
/// <param name="HomeId">Building id of the agent's home (e.g. <c>home-1</c>) from <c>seed/map.json</c>.</param>
public sealed record AgentSeed(
    Guid Id,
    string Name,
    int Age,
    IReadOnlyList<string> Traits,
    string Goal,
    string SpeakingStyle,
    string? JobId,
    string HomeId,
    decimal Money);
