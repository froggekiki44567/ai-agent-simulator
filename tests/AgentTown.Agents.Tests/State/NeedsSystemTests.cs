using AgentTown.Agents.State;
using AgentTown.Contracts.Model;
using FluentAssertions;
using Xunit;

namespace AgentTown.Agents.Tests.State;

public class NeedsSystemTests
{
    private readonly NeedsSystem _needs = new(new NeedsOptions
    {
        HungerDecayPerTick = 2,
        EnergyDecayPerTick = 1,
        SocialDecayPerTick = 0.5,
        SleepEnergyPerTick = 3,
        EatHungerGain = 50,
        TalkSocialGain = 25,
    });

    [Fact]
    public void Needs_drop_every_tick_while_awake()
    {
        var agent = TestAgents.Create(hunger: 50, energy: 50, social: 50);

        _needs.ApplyTick(agent);

        agent.Hunger.Should().Be(48);
        agent.Energy.Should().Be(49);
        agent.Social.Should().Be(49.5);
    }

    [Fact]
    public void Sleeping_restores_energy_but_hunger_still_drops()
    {
        var agent = TestAgents.Create(hunger: 50, energy: 50);
        agent.Status = AgentStatus.Sleeping;

        _needs.ApplyTick(agent);

        agent.Energy.Should().Be(53);
        agent.Hunger.Should().Be(48);
    }

    [Fact]
    public void Needs_never_go_below_zero_or_above_100()
    {
        var hungry = TestAgents.Create(hunger: 1, energy: 0.5, social: 0);
        _needs.ApplyTick(hungry);
        hungry.Hunger.Should().Be(0);
        hungry.Energy.Should().Be(0);
        hungry.Social.Should().Be(0);

        var full = TestAgents.Create(hunger: 90);
        _needs.ApplyActionCompleted(full, ActionType.Eat);
        full.Hunger.Should().Be(100);
    }

    [Theory]
    [InlineData(ActionType.Eat, 20, 50, 70, 50)]
    [InlineData(ActionType.Talk, 50, 20, 50, 45)]
    public void Actions_restore_needs(ActionType action, double hunger, double social, double expectedHunger, double expectedSocial)
    {
        var agent = TestAgents.Create(hunger: hunger, social: social);

        _needs.ApplyActionCompleted(agent, action);

        agent.Hunger.Should().Be(expectedHunger);
        agent.Social.Should().Be(expectedSocial);
    }

    [Fact]
    public void Sleep_action_puts_agent_to_sleep()
    {
        var agent = TestAgents.Create();

        _needs.ApplyActionCompleted(agent, ActionType.Sleep);

        agent.Status.Should().Be(AgentStatus.Sleeping);
    }
}
