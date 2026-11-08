using AgentTown.Agents.Conversations;
using AgentTown.Agents.Memory;
using AgentTown.Agents.Planning;
using AgentTown.Agents.State;
using AgentTown.Contracts.Events;
using AgentTown.Contracts.Model;
using AgentTown.Llm.Client;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgentTown.Agents.Tests.Memory;

public sealed class MemoryWriterTests
{
    private readonly MemoryOptions _options = new();
    private readonly InMemoryMemoryStore _store;
    private readonly MemoryWriter _writer;
    private readonly AgentState _jonas = TestAgents.Create(name: "Jonas");
    private readonly AgentState _ona = TestAgents.Create(name: "Ona");

    public MemoryWriterTests()
    {
        _store = new InMemoryMemoryStore(_options);
        _writer = new MemoryWriter(_store, new FakeLlmClient(), _options, NullLogger<MemoryWriter>.Instance);
    }

    private async Task<IReadOnlyList<MemoryRecord>> MemoriesOf(AgentState agent) => await _store.GetByAgentAsync(agent.Id);

    private static ActionResult Ok(AgentState agent, PendingAction action) => new(agent.Id, action.RequestId, true, null, AgentStateDelta.None);

    [Fact]
    public async Task Stores_text_with_embedding_and_clamped_importance()
    {
        var memory = await _writer.RememberAsync(_jonas.Id, MemoryKind.Event, "  Big news!  ", importance: 15, tick: 42);

        memory!.Text.Should().Be("Big news!");
        memory.Importance.Should().Be(10);
        memory.Embedding.Should().HaveCount(FakeLlmClient.EmbeddingDimensions);
        memory.LastAccessedTick.Should().Be(42);
        (await MemoriesOf(_jonas)).Should().ContainSingle();
    }

    [Fact]
    public async Task Importance_zero_is_not_stored()
    {
        (await _writer.RememberAsync(_jonas.Id, MemoryKind.Observation, "I walked.", 0, 1)).Should().BeNull();
        (await MemoriesOf(_jonas)).Should().BeEmpty();
    }

    [Fact]
    public async Task Successful_purchase_is_remembered_with_price()
    {
        var buy = new PendingAction(Guid.NewGuid(), ActionType.Buy, "bakery-1", 5, "bread", 2);

        var memory = await _writer.RememberActionResultAsync(_jonas, buy, Ok(_jonas, buy), moneyChange: -6m, targetName: null, tick: 6);

        memory!.Text.Should().Be("I bought 2 bread at bakery-1 for 6.00 EUR.");
        memory.Importance.Should().Be(MemoryImportance.Purchase);
    }

    [Fact]
    public async Task Routine_success_is_skipped_but_failure_is_remembered()
    {
        var move = new PendingAction(Guid.NewGuid(), ActionType.Move, "bank-1", 5);
        var buy = new PendingAction(Guid.NewGuid(), ActionType.Buy, "shop-1", 5, "milk", 1);

        (await _writer.RememberActionResultAsync(_jonas, move, Ok(_jonas, move), 0, null, 6)).Should().BeNull();
        var failed = await _writer.RememberActionResultAsync(_jonas, buy,
            new ActionResult(_jonas.Id, buy.RequestId, false, "Shop is closed", AgentStateDelta.None), 0, null, 6);

        failed!.Text.Should().Be("I tried to buy milk at shop-1 but it failed: Shop is closed.");
        failed.Importance.Should().Be(MemoryImportance.FailedAction);
    }

    [Fact]
    public async Task Llm_decision_uses_the_models_importance_and_rules_are_skipped()
    {
        var request = new ActionRequested(Guid.NewGuid(), _jonas.Id, ActionType.Deposit, "bank-1", null, 0, 50, null, "save", DecisionSource.Llm);
        var llm = new AgentDecision(request, "I want to save for a bakery of my own.", 7, null, null);
        var rules = llm with { Request = request with { Source = DecisionSource.Rules } };

        var memory = await _writer.RememberDecisionAsync(_jonas, llm, 10);
        (await _writer.RememberDecisionAsync(_jonas, rules, 10)).Should().BeNull();

        memory!.Importance.Should().Be(7);
        memory.Text.Should().Be("I decided to deposit: I want to save for a bakery of my own.");
    }

    [Fact]
    public async Task Both_sides_remember_a_conversation_from_their_point_of_view()
    {
        var id = Guid.NewGuid();
        var outcome = new ConversationOutcome(id, _jonas.Id, _ona.Id,
            [new(id, _jonas.Id, _ona.Id, "Coffee tomorrow?", 0, false), new(id, _ona.Id, _jonas.Id, "Yes, at nine!", 1, true)],
            "meet at the cafe at 9", 3, 2, null);

        await _writer.RememberConversationAsync(outcome, _jonas, _ona, 70);

        var jonas = (await MemoriesOf(_jonas)).Single();
        jonas.Kind.Should().Be(MemoryKind.Conversation);
        jonas.Importance.Should().Be(MemoryImportance.ConversationWithAgreement);
        jonas.Text.Should().Be("I talked with Ona. I: \"Coffee tomorrow?\" Ona: \"Yes, at nine!\" We agreed: meet at the cafe at 9.");
        (await MemoriesOf(_ona)).Single().Text.Should().StartWith("I talked with Jonas. Jonas: \"Coffee tomorrow?\" I: \"Yes, at nine!\"");
    }

    [Fact]
    public async Task Wage_is_remembered_but_own_purchase_transaction_is_not()
    {
        var wage = new TransactionCompleted(Guid.NewGuid(), Guid.NewGuid(), _jonas.Id, 80m, TransactionKinds.Wage, "Bakery shift", 96);
        var purchase = new TransactionCompleted(Guid.NewGuid(), _jonas.Id, Guid.NewGuid(), 3m, TransactionKinds.Purchase, null, 97);

        var memory = await _writer.RememberTransactionAsync(_jonas, wage, received: true);
        (await _writer.RememberTransactionAsync(_jonas, purchase, received: false)).Should().BeNull();

        memory!.Text.Should().Be("I got paid 80.00 EUR for my work. (Bakery shift)");
        memory.Tick.Should().Be(96);
    }

    [Fact]
    public async Task Embedding_failure_still_stores_the_memory()
    {
        var writer = new MemoryWriter(_store, new FailingEmbeddingClient(), _options, NullLogger<MemoryWriter>.Instance);

        var memory = await writer.RememberBankruptcyAsync(_jonas, 200);

        memory!.Embedding.Should().BeNull();
        memory.Importance.Should().Be(MemoryImportance.Bankruptcy);
    }

    [Fact]
    public async Task Store_forgets_least_important_oldest_memories_above_the_limit()
    {
        _options.MaxMemoriesPerAgent = 3;
        await _writer.RememberAsync(_jonas.Id, MemoryKind.Observation, "old trivial", 1, 1);
        await _writer.RememberAsync(_jonas.Id, MemoryKind.Observation, "old important", 9, 2);
        await _writer.RememberAsync(_jonas.Id, MemoryKind.Observation, "new trivial", 1, 3);
        await _writer.RememberAsync(_jonas.Id, MemoryKind.Observation, "new medium", 4, 4);

        (await MemoriesOf(_jonas)).Select(m => m.Text).Should().BeEquivalentTo("old important", "new trivial", "new medium");
    }

    [Theory]
    [InlineData(ActionType.Buy, true, 2)]
    [InlineData(ActionType.Move, true, 0)]
    [InlineData(ActionType.Work, true, 0)]
    [InlineData(ActionType.TakeLoan, true, 5)]
    [InlineData(ActionType.Move, false, 2)]
    [InlineData(ActionType.Buy, false, 3)]
    [InlineData(ActionType.Idle, false, 0)]
    public void Action_importance_heuristic(ActionType action, bool success, int expected) =>
        MemoryImportance.ForAction(action, success).Should().Be(expected);
}
