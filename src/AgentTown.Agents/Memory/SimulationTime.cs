using AgentTown.Contracts.Events;

namespace AgentTown.Agents.Memory;

public static class SimulationTime
{
    public static double HoursBetween(long fromTick, long toTick) =>
        Math.Max(0, toTick - fromTick) * TickOccurred.MinutesPerTick / 60.0;

    /// <summary>"day 2, 09:30" for a tick (tick 0 = day 1, 00:00).</summary>
    public static string Describe(long tick)
    {
        var minutes = Math.Max(0, tick) * TickOccurred.MinutesPerTick;
        return $"day {minutes / (24 * 60) + 1}, {minutes / 60 % 24:00}:{minutes % 60:00}";
    }
}
