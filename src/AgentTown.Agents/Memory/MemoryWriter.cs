using System.Globalization;
using AgentTown.Agents.Conversations;
using AgentTown.Agents.Planning;
using AgentTown.Agents.State;
using AgentTown.Contracts.Events;
using AgentTown.Contracts.Model;
using AgentTown.Llm.Client;
using Microsoft.Extensions.Logging;

namespace AgentTown.Agents.Memory;

/// <summary>
/// Turns what happens to an agent (action results, conversations, transactions, LLM decisions) into
/// first-person memories with an embedding (<see cref="ILlmClient.EmbedAsync"/>).
/// Never throws: a memory that can't be embedded is stored without an embedding.
/// </summary>
public sealed class MemoryWriter(IMemoryStore store, ILlmClient llm, MemoryOptions options, ILogger<MemoryWriter> logger)
{
    /// <summary>Stores one memory. Importance is clamped to 1–10; 0 or less means "skip" and returns null.</summary>
    public async Task<MemoryRecord?> RememberAsync(Guid agentId, MemoryKind kind, string text, int importance, long tick, CancellationToken cancellationToken = default)
    {
        if (importance <= 0 || string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            var memory = new MemoryRecord
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                Tick = tick,
                Kind = kind,
                Text = text.Trim(),
                Importance = Math.Clamp(importance, 1, 10),
                Embedding = await TryEmbedAsync(text, cancellationToken),
                LastAccessedTick = tick,
            };
            await store.AddAsync(memory, cancellationToken);
            return memory;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not store a memory for {AgentId}.", agentId);
            return null;
        }
    }

    public Task<MemoryRecord?> RememberActionResultAsync(
        AgentState agent, PendingAction action, ActionResult result, decimal moneyChange, string? targetName, long tick,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(result);

        var importance = MemoryImportance.ForAction(action.Type, result.Success);
        if (importance == MemoryImportance.Skip)
            return Task.FromResult<MemoryRecord?>(null);

        var target = targetName ?? action.TargetId ?? "somewhere";
        var text = result.Success
            ? DescribeSuccess(action, target, moneyChange)
            : $"I tried to {Verb(action.Type)} {Describe(action, target)} but it failed: {result.FailureReason ?? "no reason given"}.";

        return RememberAsync(agent.Id, MemoryKind.Observation, text, importance, tick, cancellationToken);
    }

    /// <summary>LLM decisions are remembered with the model's own importance.</summary>
    public Task<MemoryRecord?> RememberDecisionAsync(AgentState agent, AgentDecision decision, long tick, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.Source != DecisionSource.Llm)
            return Task.FromResult<MemoryRecord?>(null);

        return RememberAsync(agent.Id, MemoryKind.Observation, $"I decided to {Verb(decision.Request.Type)}: {decision.Thought}", decision.Importance, tick, cancellationToken);
    }

    /// <summary>Both participants remember the conversation from their own side.</summary>
    public async Task RememberConversationAsync(ConversationOutcome outcome, AgentState initiator, AgentState partner, long tick, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(initiator);
        ArgumentNullException.ThrowIfNull(partner);
        if (outcome.Lines.Count == 0)
            return;

        var importance = outcome.Agreement is null ? MemoryImportance.Conversation : MemoryImportance.ConversationWithAgreement;
        await RememberAsync(initiator.Id, MemoryKind.Conversation, DescribeConversation(outcome, initiator, partner), importance, tick, cancellationToken);
        await RememberAsync(partner.Id, MemoryKind.Conversation, DescribeConversation(outcome, partner, initiator), importance, tick, cancellationToken);
    }

    public Task<MemoryRecord?> RememberTransactionAsync(AgentState agent, TransactionCompleted transaction, bool received, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(transaction);

        var importance = MemoryImportance.ForTransaction(transaction.Kind, received);
        if (importance == MemoryImportance.Skip)
            return Task.FromResult<MemoryRecord?>(null);

        var amount = Money(transaction.Amount);
        var text = transaction.Kind switch
        {
            TransactionKinds.Wage => $"I got paid {amount} EUR for my work.",
            TransactionKinds.Sale => $"I earned {amount} EUR from a sale in my shop.",
            TransactionKinds.Transfer => $"Someone sent me {amount} EUR.",
            TransactionKinds.LoanInterest => $"I paid {amount} EUR interest on my loan.",
            _ => $"{transaction.Kind}: {amount} EUR.",
        };
        if (!string.IsNullOrWhiteSpace(transaction.Memo))
            text += $" ({transaction.Memo.Trim()})";

        return RememberAsync(agent.Id, MemoryKind.Observation, text, importance, transaction.Tick, cancellationToken);
    }

    public Task<MemoryRecord?> RememberBankruptcyAsync(AgentState agent, long tick, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return RememberAsync(agent.Id, MemoryKind.Observation, "I went bankrupt. I have no money left and can't buy anything until I find work.",
            MemoryImportance.Bankruptcy, tick, cancellationToken);
    }

    private async Task<float[]?> TryEmbedAsync(string text, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.EmbeddingTimeout);
        try
        {
            return await llm.EmbedAsync(text, timeout.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Embedding failed; storing memory without it.");
            return null;
        }
    }

    private static string DescribeSuccess(PendingAction action, string target, decimal moneyChange) => action.Type switch
    {
        ActionType.Buy => $"I bought {action.Quantity} {action.ItemId} at {target}{Cost(-moneyChange)}.",
        ActionType.Sell => $"I sold {action.Quantity} {action.ItemId} at {target}{Cost(moneyChange)}.",
        ActionType.Eat => $"I ate {action.ItemId}.",
        ActionType.Deposit => $"I deposited {Money(action.Amount)} EUR at {target}.",
        ActionType.Withdraw => $"I withdrew {Money(action.Amount)} EUR at {target}.",
        ActionType.Transfer => $"I gave {target} {Money(action.Amount)} EUR.",
        ActionType.TakeLoan => $"I took a loan of {Money(action.Amount)} EUR from {target}.",
        _ => $"I {Verb(action.Type)} {target}.",
    };

    private static string Describe(PendingAction action, string target) => action.Type switch
    {
        ActionType.Buy or ActionType.Sell => $"{action.ItemId} at {target}",
        ActionType.Eat => action.ItemId ?? "something",
        ActionType.Move => $"to {target}",
        ActionType.Talk => $"with {target}",
        _ => $"at {target}",
    };

    private static string Verb(ActionType action) => action switch
    {
        ActionType.Move => "go",
        ActionType.Talk => "talk",
        ActionType.TakeLoan => "take a loan",
        _ => action.ToString().ToLowerInvariant(),
    };

    private static string DescribeConversation(ConversationOutcome outcome, AgentState me, AgentState other)
    {
        var lines = string.Join(" ", outcome.Lines.Select(l => $"{(l.SpeakerId == me.Id ? "I" : other.Name)}: \"{l.Text}\""));
        var text = $"I talked with {other.Name}. {lines}";
        return outcome.Agreement is null ? text : $"{text} We agreed: {outcome.Agreement}.";
    }

    private static string Cost(decimal amount) => amount > 0 ? $" for {Money(amount)} EUR" : "";

    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
}
