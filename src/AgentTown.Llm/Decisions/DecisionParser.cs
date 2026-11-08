using System.Text.Json;
using System.Text.Json.Serialization;
using AgentTown.Contracts.Events;
using AgentTown.Contracts.Model;
using AgentTown.Llm.Client;

namespace AgentTown.Llm.Decisions;

/// <summary>
/// Turns the model's raw JSON into a checked <see cref="LlmDecision"/>.
/// Anything malformed or impossible is rejected, so the caller falls back to the rule-based planner.
/// This only checks what the agent can know (format, known ids); World still checks the real rules (ADR-002).
/// </summary>
public sealed class DecisionParser
{
    public const int DefaultImportance = 3;
    public const int MaxThoughtLength = 300;
    public const int MaxSayLength = 300;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly HashSet<ActionType> NeedsTarget =
        [ActionType.Move, ActionType.Work, ActionType.Buy, ActionType.Sell, ActionType.Sleep, ActionType.Talk,
         ActionType.Deposit, ActionType.Withdraw, ActionType.Transfer, ActionType.TakeLoan];

    private static readonly HashSet<ActionType> NeedsItem = [ActionType.Buy, ActionType.Sell, ActionType.Eat];

    private static readonly HashSet<ActionType> NeedsAmount =
        [ActionType.Deposit, ActionType.Withdraw, ActionType.Transfer, ActionType.TakeLoan];

    /// <param name="knownTargets">Building and agent ids the agent was told about in the prompt.</param>
    public DecisionParseResult Parse(string? raw, IReadOnlySet<string> knownTargets)
    {
        ArgumentNullException.ThrowIfNull(knownTargets);

        if (string.IsNullOrWhiteSpace(raw))
            return DecisionParseResult.Fail("Empty response.");

        RawDecision? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<RawDecision>(JsonText.Extract(raw), JsonOptions);
        }
        catch (JsonException ex)
        {
            return DecisionParseResult.Fail($"Invalid JSON: {ex.Message}");
        }

        if (parsed is null)
            return DecisionParseResult.Fail("Response is null.");

        if (string.IsNullOrWhiteSpace(parsed.Action))
            return DecisionParseResult.Fail("Missing 'action'.");

        if (!Enum.TryParse<ActionType>(parsed.Action.Trim(), ignoreCase: true, out var action) || !Enum.IsDefined(action))
            return DecisionParseResult.Fail($"Unknown action '{parsed.Action}'.");

        var target = Clean(parsed.Target);
        var item = Clean(parsed.Item);
        var say = Truncate(Clean(parsed.Say), MaxSayLength);

        if (NeedsTarget.Contains(action))
        {
            if (target is null)
                return DecisionParseResult.Fail($"{action} needs a 'target'.");
            if (!knownTargets.Contains(target))
                return DecisionParseResult.Fail($"Unknown target '{target}'.");
        }

        if (NeedsItem.Contains(action) && item is null)
            return DecisionParseResult.Fail($"{action} needs an 'item'.");

        if (parsed.Quantity < 0)
            return DecisionParseResult.Fail("'quantity' cannot be negative.");
        var quantity = NeedsItem.Contains(action) ? Math.Max(1, parsed.Quantity) : parsed.Quantity;

        if (parsed.Amount < 0)
            return DecisionParseResult.Fail("'amount' cannot be negative.");
        if (NeedsAmount.Contains(action) && parsed.Amount == 0)
            return DecisionParseResult.Fail($"{action} needs an 'amount' greater than 0.");

        if (action == ActionType.Talk && say is null)
            return DecisionParseResult.Fail("Talk needs 'say'.");

        var thought = Truncate(Clean(parsed.Thought), MaxThoughtLength) ?? $"I decided to {action.ToString().ToLowerInvariant()}.";
        var importance = parsed.Importance is { } i ? Math.Clamp(i, 1, 10) : DefaultImportance;

        return DecisionParseResult.Ok(new LlmDecision(action, target, item, quantity, parsed.Amount, say, thought, importance));
    }

    /// <summary>Converts a decision into the message sent to World.</summary>
    public static ActionRequested ToRequest(LlmDecision decision, Guid agentId, Guid? requestId = null)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return new ActionRequested(
            requestId ?? Guid.NewGuid(), agentId, decision.Action, decision.Target, decision.Item,
            decision.Quantity, decision.Amount, decision.Say, decision.Thought, DecisionSource.Llm);
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Equals("null", StringComparison.OrdinalIgnoreCase) || trimmed == "none" ? null : trimmed;
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];

    private sealed record RawDecision(
        string? Action,
        string? Target,
        string? Item,
        int Quantity,
        decimal Amount,
        string? Say,
        string? Thought,
        int? Importance);
}
