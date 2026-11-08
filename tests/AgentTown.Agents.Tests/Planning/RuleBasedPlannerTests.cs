using AgentTown.Agents.Planning;
using AgentTown.Contracts.Model;
using FluentAssertions;
using Xunit;

namespace AgentTown.Agents.Tests.Planning;

public class RuleBasedPlannerTests
{
    private static readonly JobInfo BakerJob = new("job-baker", "bakery-1", ShiftStartHour: 8, ShiftEndHour: 16);

    private readonly RuleBasedPlanner _planner = new(new PlannerOptions());

    [Theory]
    [InlineData(23)]
    [InlineData(2)]
    [InlineData(6)]
    public void Sleeps_at_home_at_night(int hour)
    {
        var agent = TestAgents.Create(location: "home-1");

        var action = _planner.Plan(agent, hour, BakerJob);

        action.Type.Should().Be(ActionType.Sleep);
        action.TargetId.Should().Be("home-1");
    }

    [Fact]
    public void Goes_home_at_night_when_away()
    {
        var agent = TestAgents.Create(location: "cafe-1");

        var action = _planner.Plan(agent, 23, BakerJob);

        action.Should().BeEquivalentTo(new { Type = ActionType.Move, TargetId = "home-1" });
    }

    [Fact]
    public void Keeps_sleeping_during_the_night()
    {
        var agent = TestAgents.Create();
        agent.Status = AgentStatus.Sleeping;

        _planner.Plan(agent, 3, BakerJob).Type.Should().Be(ActionType.Idle);
    }

    [Fact]
    public void Wakes_up_and_goes_to_work_in_the_morning()
    {
        var agent = TestAgents.Create(location: "home-1");
        agent.Status = AgentStatus.Sleeping;

        var action = _planner.Plan(agent, 8, BakerJob);

        action.Should().BeEquivalentTo(new { Type = ActionType.Move, TargetId = "bakery-1" });
    }

    [Fact]
    public void Works_when_at_workplace_during_shift()
    {
        var agent = TestAgents.Create(location: "bakery-1");

        var action = _planner.Plan(agent, 10, BakerJob);

        action.Should().BeEquivalentTo(new { Type = ActionType.Work, TargetId = "bakery-1" });
    }

    [Fact]
    public void Does_not_work_outside_shift()
    {
        var agent = TestAgents.Create(location: "bakery-1");

        _planner.Plan(agent, 17, BakerJob).Type.Should().Be(ActionType.Idle);
    }

    [Fact]
    public void Hungry_agent_with_food_eats()
    {
        var agent = TestAgents.Create(location: "bakery-1", hunger: 20);
        agent.Inventory["bread"] = 1;

        var action = _planner.Plan(agent, 10, BakerJob);

        action.Should().BeEquivalentTo(new { Type = ActionType.Eat, ItemId = "bread", Quantity = 1 });
    }

    [Fact]
    public void Hungry_agent_without_food_goes_to_bakery_then_buys()
    {
        var atHome = TestAgents.Create(location: "home-1", hunger: 20);
        _planner.Plan(atHome, 17, BakerJob)
            .Should().BeEquivalentTo(new { Type = ActionType.Move, TargetId = "bakery-1" });

        var atBakery = TestAgents.Create(location: "bakery-1", hunger: 20);
        _planner.Plan(atBakery, 17, BakerJob)
            .Should().BeEquivalentTo(new { Type = ActionType.Buy, TargetId = "bakery-1", ItemId = "bread", Quantity = 1 });
    }

    [Fact]
    public void Hunger_beats_work()
    {
        var agent = TestAgents.Create(location: "home-1", hunger: 10);

        var action = _planner.Plan(agent, 10, BakerJob);

        action.Should().BeEquivalentTo(new { Type = ActionType.Move, TargetId = "bakery-1" });
    }

    [Fact]
    public void Hungry_agent_with_no_money_goes_to_work_instead()
    {
        var agent = TestAgents.Create(location: "home-1", hunger: 10, money: 0);

        var action = _planner.Plan(agent, 10, BakerJob);

        action.Should().BeEquivalentTo(new { Type = ActionType.Move, TargetId = "bakery-1" }, "it can't buy food, so it goes to its shift");
        action.Reason.Should().Contain("work");
    }

    [Fact]
    public void Tired_agent_goes_home_to_rest()
    {
        var agent = TestAgents.Create(location: "bakery-1", energy: 10);

        var action = _planner.Plan(agent, 12, BakerJob);

        action.Should().BeEquivalentTo(new { Type = ActionType.Move, TargetId = "home-1" });
    }

    [Fact]
    public void Lonely_agent_without_job_goes_to_park()
    {
        var agent = TestAgents.Create(location: "home-1", social: 10, jobId: null);

        var action = _planner.Plan(agent, 12, job: null);

        action.Should().BeEquivalentTo(new { Type = ActionType.Move, TargetId = "park-1" });
    }

    [Fact]
    public void Content_agent_with_nothing_to_do_idles()
    {
        var agent = TestAgents.Create(location: "home-1", jobId: null);

        _planner.Plan(agent, 12, job: null).Type.Should().Be(ActionType.Idle);
    }

    [Theory]
    [InlineData(22, true)]
    [InlineData(2, true)]
    [InlineData(6, false)]
    [InlineData(12, false)]
    public void Night_shift_crosses_midnight(int hour, bool expected)
    {
        new JobInfo("job-guard", "factory-1", ShiftStartHour: 22, ShiftEndHour: 6).IsOnShift(hour).Should().Be(expected);
    }

    [Fact]
    public void Planned_action_becomes_rules_request()
    {
        var agentId = Guid.NewGuid();
        var planned = new PlannedAction(ActionType.Buy, "hungry", "bakery-1", "bread", 1);

        var request = planned.ToRequest(agentId, DecisionSource.Rules);

        request.Should().BeEquivalentTo(new
        {
            AgentId = agentId,
            Type = ActionType.Buy,
            TargetId = "bakery-1",
            ItemId = "bread",
            Quantity = 1,
            Source = DecisionSource.Rules,
        });
        request.RequestId.Should().NotBeEmpty();
    }

    [Fact]
    public void Invalid_hour_throws()
    {
        var act = () => _planner.Plan(TestAgents.Create(), 24, BakerJob);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
