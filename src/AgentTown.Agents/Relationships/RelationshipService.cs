using System.Globalization;
using AgentTown.Agents.Conversations;
using AgentTown.Agents.Loop;
using AgentTown.Agents.Memory;
using AgentTown.Contracts.Events;
using AgentTown.Contracts.Model;
using Microsoft.Extensions.Logging;

namespace AgentTown.Agents.Relationships;

/// <summary>
/// Keeps track of how agents feel about each other.
/// <list type="bullet">
/// <item>After a conversation, each side's affinity changes by its average tone (rated by the LLM, -5…5) × <see cref="RelationshipOptions.ToneMultiplier"/>.</item>
/// <item>A transfer between two agents makes the receiver like the giver a bit more and is booked in <see cref="RelationshipRecord.Balance"/>
/// (the receiver now owes the giver; paying it back evens it out).</item>
/// </list>
/// Update methods never throw: a relationship problem must not stop the town.
/// </summary>
public sealed class RelationshipService(
    IRelationshipStore store,
    RelationshipOptions options,
    ILogger<RelationshipService> logger,
    AgentRegistry? registry = null)
{
    // Read-modify-write of two records must not interleave (conversations and transactions run in the background).
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RelationshipOptions Options { get; } = options;

    /// <summary>The other agent's name, or null if it isn't known here.</summary>
    public string? NameOf(Guid agentId) => registry?.Find(agentId)?.Name;

    public Task<RelationshipRecord?> GetAsync(Guid agentId, Guid otherAgentId, CancellationToken cancellationToken = default) =>
        store.GetAsync(agentId, otherAgentId, cancellationToken);

    public Task<IReadOnlyList<RelationshipRecord>> GetForAgentAsync(Guid agentId, CancellationToken cancellationToken = default) =>
        store.GetByAgentAsync(agentId, cancellationToken);

    public async Task ApplyConversationAsync(ConversationOutcome outcome, long tick, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (outcome.Lines.Count == 0 || outcome.InitiatorId == outcome.PartnerId)
            return;

        await UpdateAsync(async () =>
        {
            await ChangeAsync(outcome.InitiatorId, outcome.PartnerId, ToneDelta(outcome.InitiatorTone), 0, tick, cancellationToken);
            await ChangeAsync(outcome.PartnerId, outcome.InitiatorId, ToneDelta(outcome.PartnerTone), 0, tick, cancellationToken);
        }, "conversation", cancellationToken);
    }

    /// <summary>
    /// Handles money moving from one agent to another. Only <see cref="TransactionKinds.Transfer"/> counts:
    /// purchases, wages and bank transactions don't say anything about a relationship.
    /// </summary>
    public async Task ApplyTransferAsync(Guid fromAgentId, Guid toAgentId, TransactionCompleted transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (fromAgentId == toAgentId || transaction.Amount <= 0 || transaction.Kind != TransactionKinds.Transfer)
            return;

        await UpdateAsync(async () =>
        {
            await ChangeAsync(toAgentId, fromAgentId, TransferDelta(transaction.Amount), -transaction.Amount, transaction.Tick, cancellationToken);
            await ChangeAsync(fromAgentId, toAgentId, 0, transaction.Amount, transaction.Tick, cancellationToken);
        }, "transfer", cancellationToken);
    }

    /// <summary>
    /// A short description for prompts, e.g. "a good friend who owes you 20.00 EUR".
    /// <paramref name="ownerName"/> is the agent whose view this is; null writes it as "you".
    /// </summary>
    public string Describe(RelationshipRecord relationship, string? ownerName = null)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        var label = Label(relationship.Affinity);
        var owner = ownerName ?? "you";
        var amount = Math.Abs(relationship.Balance).ToString("0.00", CultureInfo.InvariantCulture);

        if (relationship.Balance >= Options.DebtMentionAbove)
            return $"{label} who owes {owner} {amount} EUR";
        if (relationship.Balance <= -Options.DebtMentionAbove)
            return $"{label} whom {owner} owe{(ownerName is null ? "" : "s")} {amount} EUR";
        return label;
    }

    /// <summary>How this agent sees the other, or null if they never met.</summary>
    public async Task<string?> DescribeAsync(Guid agentId, Guid otherAgentId, string? ownerName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var relationship = await store.GetAsync(agentId, otherAgentId, cancellationToken);
            return relationship is null ? null : Describe(relationship, ownerName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not read relationship {AgentId} → {OtherId}.", agentId, otherAgentId);
            return null;
        }
    }

    public static string Label(int affinity) => affinity switch
    {
        >= 60 => "a close friend",
        >= 25 => "a good friend",
        >= 10 => "a friendly acquaintance",
        > -10 => "an acquaintance",
        > -25 => "an unpleasant acquaintance",
        > -60 => "a rival",
        _ => "an enemy",
    };

    public int ToneDelta(double averageTone) =>
        (int)Math.Round(Math.Clamp(averageTone, -5, 5) * Options.ToneMultiplier, MidpointRounding.AwayFromZero);

    public int TransferDelta(decimal amount)
    {
        var perPoint = Options.TransferEurPerAffinity <= 0 ? 10m : Options.TransferEurPerAffinity;
        return (int)Math.Clamp(Math.Ceiling(amount / perPoint), 1, Math.Max(1, Options.MaxTransferAffinity));
    }

    private async Task UpdateAsync(Func<Task> update, string what, CancellationToken cancellationToken)
    {
        try
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                await update();
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not update relationships after a {What}.", what);
        }
    }

    private async Task ChangeAsync(Guid agentId, Guid otherId, int affinityDelta, decimal balanceDelta, long tick, CancellationToken cancellationToken)
    {
        var current = await store.GetAsync(agentId, otherId, cancellationToken)
            ?? new RelationshipRecord(agentId, otherId, 0, tick);

        await store.UpsertAsync(current with
        {
            Affinity = Math.Clamp(current.Affinity + affinityDelta, RelationshipRecord.MinAffinity, RelationshipRecord.MaxAffinity),
            Balance = current.Balance + balanceDelta,
            LastInteractionTick = Math.Max(current.LastInteractionTick, tick),
        }, cancellationToken);
    }

    /// <summary>"day 2, 14:00" for the last time they met (used in prompts).</summary>
    public static string LastMet(RelationshipRecord relationship) => SimulationTime.Describe(relationship.LastInteractionTick);
}
