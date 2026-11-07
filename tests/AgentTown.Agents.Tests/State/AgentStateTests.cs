using AgentTown.Contracts.Model;
using FluentAssertions;
using Xunit;

namespace AgentTown.Agents.Tests.State;

public class AgentStateTests
{
    [Fact]
    public void Apply_changes_only_fields_that_are_set()
    {
        var agent = TestAgents.Create(location: "home-1", money: 100, hunger: 50);

        agent.Apply(new AgentStateDelta(Money: 94m, X: 3, Y: 4, LocationId: "bakery-1"));

        agent.Money.Should().Be(94m);
        agent.X.Should().Be(3);
        agent.Y.Should().Be(4);
        agent.LocationId.Should().Be("bakery-1");
        agent.Hunger.Should().Be(50, "hunger was not in the delta");
        agent.Status.Should().Be(AgentStatus.Active);
    }

    [Fact]
    public void Apply_with_empty_delta_changes_nothing()
    {
        var agent = TestAgents.Create(money: 100);

        agent.Apply(AgentStateDelta.None);

        agent.Money.Should().Be(100);
        agent.LocationId.Should().Be("home-1");
    }

    [Fact]
    public void Apply_clamps_needs_to_0_100()
    {
        var agent = TestAgents.Create();

        agent.Apply(new AgentStateDelta(Hunger: 150, Energy: -20));

        agent.Hunger.Should().Be(100);
        agent.Energy.Should().Be(0);
    }

    [Fact]
    public void Agent_with_pending_action_is_busy()
    {
        var agent = TestAgents.Create();
        agent.IsBusy.Should().BeFalse();

        agent.PendingAction = new(Guid.NewGuid(), ActionType.Move, "bakery-1", RequestedAtTick: 5);

        agent.IsBusy.Should().BeTrue();
    }
}
