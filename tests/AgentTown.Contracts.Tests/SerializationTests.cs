using System.Text.Json;
using AgentTown.Contracts.Events;
using AgentTown.Contracts.Model;
using FluentAssertions;

namespace AgentTown.Contracts.Tests;

public class SerializationTests
{
    private static readonly Guid AgentA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AgentB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static TheoryData<object> AllRecords() => new()
    {
        new TickOccurred(145, 2, 0, 10),
        new SimulationControl(SimulationCommand.SetSpeed, 4),
        new ActionRequested(Guid.NewGuid(), AgentA, ActionType.Buy, "bakery-1", "bread", 2, 0m, null, "I'm hungry", DecisionSource.Llm),
        new ActionResult(AgentA, Guid.NewGuid(), false, "Shop is closed", AgentStateDelta.None),
        new ActionResult(AgentA, Guid.NewGuid(), true, null, new AgentStateDelta(Money: 94.5m, X: 3, Y: 4, Hunger: 80, Status: AgentStatus.Active)),
        new AgentMoved(AgentA, 1, 1, 1, 2),
        new TransactionCompleted(Guid.NewGuid(), AgentA, AgentB, 999.99m, TransactionKinds.Transfer, "rent", 300),
        new ConversationMessage(Guid.NewGuid(), AgentA, AgentB, "Good morning!", 0, false),
        new AgentThought(AgentA, "Time to work.", ActionType.Work, DecisionSource.Rules),
        new WorldEventInjected(Guid.NewGuid(), "BreadPriceMultiplier", new Dictionary<string, string> { ["multiplier"] = "2" }, "Bread prices doubled"),
        new WorldEventApplied(Guid.NewGuid(), "BankCollapse", "The bank collapsed", [AgentA, AgentB], ["bank-1"], 500),
        new WorldSnapshot(
            10,
            [new AgentSnapshot(AgentA, "Ona", 3, 4, 100m, 70, 60, 50, AgentStatus.Sleeping, "home-1")],
            [new BuildingSnapshot("bakery-1", BuildingType.Bakery, "Bakery", 10, 5, 7, 19, true)]),
    };

    [Theory]
    [MemberData(nameof(AllRecords))]
    public void Record_round_trips_through_json(object record)
    {
        var json = JsonSerializer.Serialize(record, record.GetType(), ContractsJson.Options);
        var back = JsonSerializer.Deserialize(json, record.GetType(), ContractsJson.Options);

        back.Should().BeEquivalentTo(record, o => o.RespectingRuntimeTypes());
    }

    [Theory]
    [MemberData(nameof(AllRecords))]
    public void Record_round_trips_inside_envelope(object record)
    {
        var envelopeType = typeof(Envelope<>).MakeGenericType(record.GetType());
        var envelope = Activator.CreateInstance(envelopeType, Guid.NewGuid(), Guid.NewGuid(), 42L, DateTimeOffset.UtcNow, record)!;

        var json = JsonSerializer.Serialize(envelope, envelopeType, ContractsJson.Options);
        var back = JsonSerializer.Deserialize(json, envelopeType, ContractsJson.Options);

        back.Should().BeEquivalentTo(envelope, o => o.RespectingRuntimeTypes());
    }

    [Fact]
    public void Enums_are_serialized_as_strings()
    {
        var json = JsonSerializer.Serialize(new AgentThought(AgentA, "x", ActionType.Sleep, DecisionSource.Llm), ContractsJson.Options);

        json.Should().Contain("\"action\":\"Sleep\"").And.Contain("\"source\":\"Llm\"");
    }

    [Fact]
    public void Envelope_create_sets_ids_and_time()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));

        var envelope = Envelope<TickOccurred>.Create(new TickOccurred(1, 1, 0, 10), tick: 1, clock: clock);

        envelope.MessageId.Should().NotBeEmpty();
        envelope.CorrelationId.Should().Be(envelope.MessageId);
        envelope.OccurredAt.Should().Be(clock.Now);
    }

    [Fact]
    public void Envelope_create_keeps_given_correlation_id()
    {
        var correlationId = Guid.NewGuid();

        var envelope = Envelope<AgentMoved>.Create(new AgentMoved(AgentA, 0, 0, 0, 1), tick: 5, correlationId);

        envelope.CorrelationId.Should().Be(correlationId);
        envelope.MessageId.Should().NotBe(correlationId);
    }

    [Fact]
    public void Action_result_subject_contains_agent_id()
    {
        Subjects.ActionResult(AgentA).Should().Be("town.actions.result.11111111111111111111111111111111");
        Subjects.ActionResult(AgentA).Should().StartWith(Subjects.ActionResultAll.TrimEnd('*'));
    }

    [Fact]
    public void One_simulation_day_is_144_ticks()
    {
        TickOccurred.TicksPerDay.Should().Be(144);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
