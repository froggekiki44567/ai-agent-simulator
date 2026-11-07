using AgentTown.Contracts.Events;
using AgentTown.Contracts.Model;
using FluentAssertions;
using Xunit;

namespace AgentTown.Agents.Tests.Loop;

public sealed class AgentLoopTests : IDisposable
{
    private readonly LoopHarness _h = new();

    [Fact]
    public async Task Tick_sends_an_action_and_marks_agent_busy()
    {
        var jonas = TestAgents.Create(location: "home-1");
        _h.Registry.Add(jonas);

        await _h.Loop.OnTickAsync(LoopHarness.At(9));

        var request = _h.Publisher.Actions.Should().ContainSingle().Subject;
        request.Should().BeEquivalentTo(new { AgentId = jonas.Id, Type = ActionType.Move, TargetId = "bakery-1", Source = DecisionSource.Rules });
        jonas.PendingAction!.RequestId.Should().Be(request.RequestId);
    }

    [Fact]
    public async Task Busy_agent_is_not_asked_again()
    {
        _h.Registry.Add(TestAgents.Create(location: "home-1"));

        await _h.Loop.OnTickAsync(LoopHarness.At(9));
        await _h.Loop.OnTickAsync(LoopHarness.At(9, 10));
        await _h.Loop.OnTickAsync(LoopHarness.At(9, 20));

        _h.Publisher.Actions.Should().HaveCount(1, "the Move is still running");
    }

    [Fact]
    public async Task Needs_drop_every_tick_even_while_busy()
    {
        var jonas = TestAgents.Create(hunger: 80);
        _h.Registry.Add(jonas);

        await _h.Loop.OnTickAsync(LoopHarness.At(9));
        await _h.Loop.OnTickAsync(LoopHarness.At(9, 10));

        jonas.Hunger.Should().BeLessThan(80);
    }

    [Fact]
    public async Task Successful_result_updates_state_and_frees_agent()
    {
        var jonas = TestAgents.Create(location: "home-1");
        _h.Registry.Add(jonas);
        await _h.Loop.OnTickAsync(LoopHarness.At(9));
        var request = _h.Publisher.Actions.Single();

        _h.Loop.OnActionResult(new ActionResult(jonas.Id, request.RequestId, true, null, new AgentStateDelta(X: 10, Y: 5, LocationId: "bakery-1")));

        jonas.IsBusy.Should().BeFalse();
        jonas.LocationId.Should().Be("bakery-1");
        jonas.LastActionFailed.Should().BeFalse();

        await _h.Loop.OnTickAsync(LoopHarness.At(9, 10));
        _h.Publisher.Actions.Last().Type.Should().Be(ActionType.Work, "now it is at the bakery during its shift");
    }

    [Fact]
    public async Task Buy_then_eat_updates_inventory_and_hunger()
    {
        var jonas = TestAgents.Create(location: "bakery-1", hunger: 20, money: 50);
        _h.Registry.Add(jonas);

        await _h.Loop.OnTickAsync(LoopHarness.At(17));
        var buy = _h.Publisher.Actions.Last();
        buy.Type.Should().Be(ActionType.Buy);
        _h.Loop.OnActionResult(new ActionResult(jonas.Id, buy.RequestId, true, null, new AgentStateDelta(Money: 47)));
        jonas.ItemCount("bread").Should().Be(1);

        await _h.Loop.OnTickAsync(LoopHarness.At(17, 10));
        var eat = _h.Publisher.Actions.Last();
        eat.Type.Should().Be(ActionType.Eat);
        var hungerBefore = jonas.Hunger;
        _h.Loop.OnActionResult(new ActionResult(jonas.Id, eat.RequestId, true, null, AgentStateDelta.None));

        jonas.ItemCount("bread").Should().Be(0);
        jonas.Hunger.Should().BeGreaterThan(hungerBefore);
        jonas.Money.Should().Be(47);
    }

    [Fact]
    public async Task Failed_result_marks_agent_so_it_tries_something_else()
    {
        var jonas = TestAgents.Create(location: "home-1");
        _h.Registry.Add(jonas);
        await _h.Loop.OnTickAsync(LoopHarness.At(9));
        var request = _h.Publisher.Actions.Single();

        _h.Loop.OnActionResult(new ActionResult(jonas.Id, request.RequestId, false, "Path blocked", AgentStateDelta.None));

        jonas.IsBusy.Should().BeFalse();
        jonas.LastActionFailed.Should().BeTrue();
        jonas.LocationId.Should().Be("home-1");
    }

    [Fact]
    public async Task Stale_or_unknown_results_are_ignored()
    {
        var jonas = TestAgents.Create(location: "home-1");
        _h.Registry.Add(jonas);
        await _h.Loop.OnTickAsync(LoopHarness.At(9));

        _h.Loop.OnActionResult(new ActionResult(jonas.Id, Guid.NewGuid(), true, null, new AgentStateDelta(LocationId: "park-1")));
        _h.Loop.OnActionResult(new ActionResult(Guid.NewGuid(), Guid.NewGuid(), true, null, AgentStateDelta.None));

        jonas.IsBusy.Should().BeTrue("the real result hasn't arrived yet");
        jonas.LocationId.Should().Be("home-1");
    }

    [Fact]
    public async Task Missing_result_times_out_and_agent_decides_again()
    {
        using var h = new LoopHarness(pendingTimeoutTicks: 3);
        var jonas = TestAgents.Create(location: "home-1");
        h.Registry.Add(jonas);
        h.Llm.Enqueue("""{"action":"Move","target":"bakery-1","thought":"That took too long, let me try again."}""");

        for (var t = 0; t < 4; t++)
        {
            await h.Loop.OnTickAsync(LoopHarness.Tick(LoopHarness.At(9).Tick + t));
            await h.Loop.WaitForDecisionsAsync();
        }

        // After the timeout the agent counts as "failed", which is an interesting moment for the LLM.
        h.Publisher.Actions.Should().HaveCount(2);
        h.Publisher.Actions[1].Source.Should().Be(DecisionSource.Llm);
        jonas.PendingAction!.RequestId.Should().Be(h.Publisher.Actions[1].RequestId);
    }

    [Fact]
    public async Task Idle_is_not_sent_to_world()
    {
        _h.Registry.Add(TestAgents.Create(location: "home-1", jobId: null));

        await _h.Loop.OnTickAsync(LoopHarness.At(12));

        _h.Publisher.Actions.Should().BeEmpty();
    }

    [Fact]
    public async Task Llm_decision_publishes_a_thought()
    {
        var ona = TestAgents.Create(location: "park-1", jobId: null);
        var jonas = TestAgents.Create(location: "park-1", social: 20, jobId: null);
        _h.Registry.Add(ona);
        _h.Registry.Add(jonas);
        _h.Llm.Responder = p => p.User.Contains($"(id {ona.Id})")
            ? $$"""{"action":"Talk","target":"{{ona.Id}}","say":"Hello!","thought":"I want company.","importance":4}"""
            : """{"action":"Idle","thought":"Just relaxing."}""";

        await _h.Loop.OnTickAsync(LoopHarness.At(12));
        await _h.Loop.WaitForDecisionsAsync();

        _h.Publisher.Thoughts.Should().Contain(t => t.AgentId == jonas.Id && t.Text == "I want company." && t.Source == DecisionSource.Llm);
        _h.Publisher.Actions.Should().Contain(a => a.AgentId == jonas.Id && a.Type == ActionType.Talk);
    }

    [Fact]
    public async Task Slow_llm_does_not_block_other_agents()
    {
        _h.Llm.Delay = TimeSpan.FromMilliseconds(300);
        var lonely = TestAgents.Create(location: "park-1", social: 20, jobId: null);
        var other = TestAgents.Create(location: "park-1", social: 90, jobId: null);
        var worker = TestAgents.Create(location: "home-2");
        _h.Registry.Add(lonely);
        _h.Registry.Add(other);
        _h.Registry.Add(worker);

        await _h.Loop.OnTickAsync(LoopHarness.At(10));

        _h.Publisher.Actions.Should().Contain(a => a.AgentId == worker.Id, "rule decisions don't wait for the LLM");
        _h.Loop.DecidingCount.Should().BeGreaterThan(0);

        await _h.Loop.WaitForDecisionsAsync();
        _h.Loop.DecidingCount.Should().Be(0);
    }

    [Fact]
    public async Task One_failing_agent_does_not_stop_the_others()
    {
        var broken = TestAgents.Create(location: "home-1");
        var fine = TestAgents.Create(location: "home-2");
        _h.Registry.Add(broken);
        _h.Registry.Add(fine);
        _h.Publisher.FailWith = a => a.AgentId == broken.Id ? new InvalidOperationException("NATS down") : null;

        await _h.Loop.OnTickAsync(LoopHarness.At(9));

        _h.Publisher.Actions.Should().ContainSingle(a => a.AgentId == fine.Id);
        broken.IsBusy.Should().BeFalse();
    }

    public void Dispose() => _h.Dispose();
}
