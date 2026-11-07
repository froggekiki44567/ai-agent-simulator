using AgentTown.Contracts.Model;

namespace AgentTown.Contracts.Events;

/// <summary>
/// Published by World on every tick. World is the single source of time.
/// 1 tick = <see cref="MinutesPerTick"/> simulation minutes.
/// </summary>
public sealed record TickOccurred(long Tick, int Day, int Hour, int Minute)
{
    public const int MinutesPerTick = 10;
    public const int TicksPerDay = 24 * 60 / MinutesPerTick;
}

/// <summary>Api → World: pause, resume or change the simulation speed.</summary>
/// <param name="Speed">Speed multiplier (1, 2, 4) for <see cref="SimulationCommand.SetSpeed"/>.</param>
public sealed record SimulationControl(SimulationCommand Command, double? Speed = null);

/// <summary>Full world state, published by World every 10 ticks.</summary>
public sealed record WorldSnapshot(
    long Tick,
    IReadOnlyList<AgentSnapshot> Agents,
    IReadOnlyList<BuildingSnapshot> Buildings);
