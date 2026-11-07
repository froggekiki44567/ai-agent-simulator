using AgentTown.Agents.State;
using AgentTown.Contracts.Events;
using AgentTown.Contracts.Model;
using FluentAssertions;
using Xunit;

namespace AgentTown.Agents.Tests.Loop;

/// <summary>
/// The Agents-side version of gate G1: agents live through a whole simulation day
/// against a simple fake World that accepts every action instantly.
/// </summary>
public sealed class DayInTownTests : IDisposable
{
    private const decimal Wage = 40m;
    private const decimal BreadPrice = 3m;
    private const long EndOfShiftTick = 16 * 60 / TickOccurred.MinutesPerTick;

    private readonly LoopHarness _h = new();
    private readonly Dictionary<Guid, int> _workTicks = [];
    private readonly HashSet<Guid> _sleptAtHome = [];

    [Fact]
    public async Task Ten_agents_live_through_one_day()
    {
        var agents = Enumerable.Range(1, 10).Select(i => TestAgents.Create(
            location: i % 2 == 0 ? "home-2" : "home-1",
            hunger: 60, energy: 90, social: 70,
            money: i == 1 ? 0 : 30,
            jobId: i <= 7 ? "job-baker" : null)).ToList();
        foreach (var agent in agents)
            _h.Registry.Add(agent);

        for (long tick = 0; tick < TickOccurred.TicksPerDay; tick++)
        {
            await _h.Loop.OnTickAsync(LoopHarness.Tick(tick));
            await _h.Loop.WaitForDecisionsAsync();
            RunFakeWorld(tick);
            if (tick == EndOfShiftTick)
                PayWages();
        }

        _h.Publisher.Actions.Should().NotBeEmpty();
        _h.Publisher.Actions.Select(a => a.Type).Should().Contain([ActionType.Move, ActionType.Work, ActionType.Sleep, ActionType.Buy, ActionType.Eat]);

        agents.Where(a => a.JobId is not null).Should().OnlyContain(a => _workTicks.GetValueOrDefault(a.Id) > 20, "workers work their shift");
        agents.Where(a => a.JobId is null).Should().OnlyContain(a => !_workTicks.ContainsKey(a.Id), "unemployed agents don't work");
        agents.Should().OnlyContain(a => _sleptAtHome.Contains(a.Id), "everyone sleeps at home at night");
        agents.Should().OnlyContain(a => a.Hunger > 0, "nobody starves when they can buy bread");
        agents.Should().OnlyContain(a => a.Money >= 0);
        _h.Loop.DecidingCount.Should().Be(0);
    }

    /// <summary>Accepts every pending action immediately, like a very simple World.</summary>
    private void RunFakeWorld(long tick)
    {
        foreach (var agent in _h.Registry.All)
        {
            if (agent.PendingAction is not { } action)
                continue;

            var delta = action.Type switch
            {
                ActionType.Move => new AgentStateDelta(LocationId: action.TargetId),
                ActionType.Buy when agent.Money >= BreadPrice * action.Quantity => new AgentStateDelta(Money: agent.Money - BreadPrice * action.Quantity),
                ActionType.Work => CountWork(agent),
                ActionType.Sleep => new AgentStateDelta(Status: AgentStatus.Sleeping),
                _ => AgentStateDelta.None,
            };
            var success = action.Type != ActionType.Buy || agent.Money >= BreadPrice * action.Quantity;

            if (action.Type == ActionType.Sleep && agent.IsAt(agent.HomeId))
                _sleptAtHome.Add(agent.Id);

            _h.Loop.OnActionResult(new ActionResult(agent.Id, action.RequestId, success, success ? null : "Not enough money", delta));
        }
    }

    private AgentStateDelta CountWork(AgentState agent)
    {
        _workTicks[agent.Id] = _workTicks.GetValueOrDefault(agent.Id) + 1;
        return AgentStateDelta.None;
    }

    /// <summary>World pays everyone who worked during the shift (08–16) when it ends.</summary>
    private void PayWages()
    {
        foreach (var agent in _h.Registry.All.Where(a => _workTicks.ContainsKey(a.Id)))
            agent.Money += Wage;
    }

    public void Dispose() => _h.Dispose();
}
