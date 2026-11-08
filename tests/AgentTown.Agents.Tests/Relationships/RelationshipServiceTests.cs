using AgentTown.Agents.Conversations;
using AgentTown.Agents.Relationships;
using AgentTown.Contracts.Events;
using AgentTown.Contracts.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgentTown.Agents.Tests.Relationships;

public class RelationshipServiceTests
{
    private static readonly Guid Jonas = Guid.NewGuid();
    private static readonly Guid Petras = Guid.NewGuid();

    private readonly InMemoryRelationshipStore _store = new();
    private readonly RelationshipService _service;

    public RelationshipServiceTests() =>
        _service = new RelationshipService(_store, new RelationshipOptions(), NullLogger<RelationshipService>.Instance);

    private static ConversationOutcome Conversation(double initiatorTone, double partnerTone) =>
        new(Guid.NewGuid(), Jonas, Petras, [new ConversationMessage(Guid.NewGuid(), Jonas, Petras, "Hi", 0, true)],
            null, initiatorTone, partnerTone, null);

    private static TransactionCompleted Transfer(Guid from, Guid to, decimal amount, long tick = 50, string kind = TransactionKinds.Transfer) =>
        new(Guid.NewGuid(), from, to, amount, kind, null, tick);

    [Fact]
    public async Task Conversation_changes_each_side_by_its_own_tone()
    {
        await _service.ApplyConversationAsync(Conversation(initiatorTone: 4, partnerTone: -2.5), tick: 30);

        (await _store.GetAsync(Jonas, Petras)).Should().BeEquivalentTo(new { Affinity = 8, LastInteractionTick = 30L });
        (await _store.GetAsync(Petras, Jonas))!.Affinity.Should().Be(-5, "-2.5 × 2 rounds away from zero");
    }

    [Fact]
    public async Task Repeated_warm_conversations_build_a_friendship_up_to_the_limit()
    {
        for (var i = 0; i < 20; i++)
            await _service.ApplyConversationAsync(Conversation(5, 5), tick: i);

        (await _store.GetAsync(Jonas, Petras))!.Affinity.Should().Be(RelationshipRecord.MaxAffinity);
    }

    [Fact]
    public async Task Empty_conversation_changes_nothing()
    {
        await _service.ApplyConversationAsync(Conversation(5, 5) with { Lines = [] }, tick: 1);

        (await _store.GetByAgentAsync(Jonas)).Should().BeEmpty();
    }

    [Fact]
    public async Task Transfer_makes_the_receiver_owe_the_giver_and_like_them_more()
    {
        await _service.ApplyTransferAsync(Jonas, Petras, Transfer(Jonas, Petras, 25m));

        (await _store.GetAsync(Petras, Jonas)).Should().BeEquivalentTo(new { Affinity = 3, Balance = -25m, LastInteractionTick = 50L });
        (await _store.GetAsync(Jonas, Petras)).Should().BeEquivalentTo(new { Affinity = 0, Balance = 25m });
    }

    [Fact]
    public async Task Paying_back_evens_out_the_debt()
    {
        await _service.ApplyTransferAsync(Jonas, Petras, Transfer(Jonas, Petras, 20m));
        await _service.ApplyTransferAsync(Petras, Jonas, Transfer(Petras, Jonas, 20m));

        (await _store.GetAsync(Jonas, Petras))!.Balance.Should().Be(0);
        (await _store.GetAsync(Petras, Jonas))!.Balance.Should().Be(0);
    }

    [Fact]
    public async Task Only_transfers_count()
    {
        await _service.ApplyTransferAsync(Jonas, Petras, Transfer(Jonas, Petras, 20m, kind: TransactionKinds.Purchase));

        (await _store.GetByAgentAsync(Jonas)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(10, 1)]
    [InlineData(11, 2)]
    [InlineData(5000, 10)]
    public void Transfer_affinity_grows_with_the_amount_and_is_capped(double amount, int expected) =>
        _service.TransferDelta((decimal)amount).Should().Be(expected);

    [Theory]
    [InlineData(80, "a close friend")]
    [InlineData(30, "a good friend")]
    [InlineData(10, "a friendly acquaintance")]
    [InlineData(0, "an acquaintance")]
    [InlineData(-20, "an unpleasant acquaintance")]
    [InlineData(-40, "a rival")]
    [InlineData(-90, "an enemy")]
    public void Labels_follow_affinity(int affinity, string label) =>
        RelationshipService.Label(affinity).Should().Be(label);

    [Fact]
    public void Description_mentions_debts_from_the_right_perspective()
    {
        var owesMe = new RelationshipRecord(Jonas, Petras, 30, 0, Balance: 20m);
        var iOwe = owesMe with { Balance = -7.5m };

        _service.Describe(owesMe).Should().Be("a good friend who owes you 20.00 EUR");
        _service.Describe(owesMe, "Jonas").Should().Be("a good friend who owes Jonas 20.00 EUR");
        _service.Describe(iOwe).Should().Be("a good friend whom you owe 7.50 EUR");
        _service.Describe(iOwe, "Jonas").Should().Be("a good friend whom Jonas owes 7.50 EUR");
        _service.Describe(owesMe with { Balance = 0.5m }).Should().Be("a good friend", "tiny amounts are not worth mentioning");
    }

    [Fact]
    public async Task Strangers_have_no_description()
    {
        (await _service.DescribeAsync(Jonas, Petras)).Should().BeNull();
    }

    [Fact]
    public async Task Parallel_updates_are_not_lost()
    {
        await Task.WhenAll(Enumerable.Range(0, 50).Select(i => Task.Run(() => _service.ApplyConversationAsync(Conversation(0.5, 0), i))));

        (await _store.GetAsync(Jonas, Petras))!.Affinity.Should().Be(50);
    }
}
