using AgentTown.Contracts.Model;

namespace AgentTown.Llm.Decisions;

/// <summary>A validated LLM decision (the JSON format from PROJECT_PLAN.md section 2.6).</summary>
public sealed record LlmDecision(
    ActionType Action,
    string? Target,
    string? Item,
    int Quantity,
    decimal Amount,
    string? Say,
    string Thought,
    int Importance);

/// <summary>Result of <see cref="DecisionParser.Parse"/>. Either a decision or an error, never both.</summary>
public sealed record DecisionParseResult(LlmDecision? Decision, string? Error)
{
    public bool Success => Decision is not null;

    public static DecisionParseResult Ok(LlmDecision decision) => new(decision, null);
    public static DecisionParseResult Fail(string error) => new(null, error);
}
