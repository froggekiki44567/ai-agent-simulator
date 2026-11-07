using AgentTown.Agents.Planning;
using AgentTown.Llm.Prompts;

namespace AgentTown.Agents.Loop;

/// <param name="Type">Building type name, e.g. "Bakery".</param>
/// <param name="OpenHour">0–23.</param>
/// <param name="CloseHour">0–24, exclusive. Equal to <paramref name="OpenHour"/> means always open.</param>
public sealed record BuildingInfo(string Id, string Type, int OpenHour, int CloseHour)
{
    public bool IsOpen(int hour) =>
        OpenHour == CloseHour
        || (OpenHour < CloseHour ? hour >= OpenHour && hour < CloseHour : hour >= OpenHour || hour < CloseHour);
}

/// <summary>
/// What the agents know about the town: buildings and jobs. Filled from <c>seed/map.json</c> /
/// <c>WorldSnapshot</c> once B's World publishes them.
/// </summary>
public sealed class TownDirectory
{
    private readonly Dictionary<string, BuildingInfo> _buildings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JobInfo> _jobs = new(StringComparer.Ordinal);

    public IReadOnlyCollection<BuildingInfo> Buildings => _buildings.Values;

    public TownDirectory AddBuilding(BuildingInfo building)
    {
        _buildings[building.Id] = building;
        return this;
    }

    public TownDirectory AddJob(JobInfo job)
    {
        _jobs[job.JobId] = job;
        return this;
    }

    public JobInfo? FindJob(string? jobId) => jobId is null ? null : _jobs.GetValueOrDefault(jobId);

    public IReadOnlyList<NearbyBuilding> DescribeBuildings(int hour) =>
        _buildings.Values.OrderBy(b => b.Id, StringComparer.Ordinal)
            .Select(b => new NearbyBuilding(b.Id, b.Type, b.IsOpen(hour)))
            .ToArray();
}
