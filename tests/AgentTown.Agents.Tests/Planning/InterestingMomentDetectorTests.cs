using AgentTown.Agents.Planning;
using AgentTown.Contracts.Model;
using FluentAssertions;
using Xunit;

namespace AgentTown.Agents.Tests.Planning;

public class InterestingMomentDetectorTests
{
    private readonly InterestingMomentDetector _detector = new(new InterestingMomentOptions { CooldownTicks = 6, LowMoneyBelow = 20, SocialChanceBelow = 50 });

    [Fact]
    public void Routine_moment_returns_null()
    {
        var agent = TestAgents.Create(social: 80, money: 100);

        _detector.Detect(agent, new MomentInput(Tick: 100, IsNight: false, NearbyAgentCount: 3)).Should().BeNull();
    }

    [Fact]
    public void Lonely_agent_with_people_nearby_is_interesting()
    {
        var agent = TestAgents.Create(social: 30);

        _detector.Detect(agent, new MomentInput(100, false, NearbyAgentCount: 1))!.Kind.Should().Be(MomentKind.SocialChance);
    }

    [Fact]
    public void Lonely_agent_alone_is_not_interesting()
    {
        var agent = TestAgents.Create(social: 30);

        _detector.Detect(agent, new MomentInput(100, false, NearbyAgentCount: 0)).Should().BeNull();
    }

    [Fact]
    public void Low_money_is_interesting()
    {
        var agent = TestAgents.Create(money: 5);

        _detector.Detect(agent, new MomentInput(100, false, 0))!.Kind.Should().Be(MomentKind.LowMoney);
    }

    [Fact]
    public void Failed_action_is_interesting()
    {
        var agent = TestAgents.Create();
        agent.LastActionFailed = true;

        _detector.Detect(agent, new MomentInput(100, false, 0))!.Kind.Should().Be(MomentKind.ActionFailed);
    }

    [Fact]
    public void Cooldown_blocks_repeated_llm_calls()
    {
        var agent = TestAgents.Create(money: 5);
        agent.LastLlmDecisionTick = 98;

        _detector.Detect(agent, new MomentInput(100, false, 0)).Should().BeNull("only 2 ticks passed");
        _detector.Detect(agent, new MomentInput(104, false, 0)).Should().NotBeNull("6 ticks passed");
    }

    [Fact]
    public void Night_and_sleep_are_not_interesting()
    {
        var agent = TestAgents.Create(money: 5);
        _detector.Detect(agent, new MomentInput(100, IsNight: true, 0)).Should().BeNull();

        agent.Status = AgentStatus.Sleeping;
        _detector.Detect(agent, new MomentInput(100, IsNight: false, 0)).Should().BeNull();
    }

    [Fact]
    public void Town_event_always_wins_even_at_night_and_during_cooldown()
    {
        var agent = TestAgents.Create();
        agent.Status = AgentStatus.Sleeping;
        agent.LastLlmDecisionTick = 99;

        var moment = _detector.Detect(agent, new MomentInput(100, IsNight: true, 0, TownEvent: "The bank collapsed."));

        moment!.Kind.Should().Be(MomentKind.TownEvent);
        moment.Description.Should().Contain("The bank collapsed.");
    }
}
