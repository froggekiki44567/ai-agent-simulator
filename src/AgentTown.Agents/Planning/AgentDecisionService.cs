using AgentTown.Agents.State;
using AgentTown.Contracts.Events;
using AgentTown.Contracts.Model;
using AgentTown.Llm.Decisions;
using AgentTown.Llm.Prompts;
using AgentTown.Llm.Scheduling;

namespace AgentTown.Agents.Planning;

/// <summary>Everything about the current moment that a decision needs.</summary>
public sealed record DecisionInput(
    long Tick,
    int Day,
    int Hour,
    int Minute,
    JobInfo? Job,
    IReadOnlyList<NearbyAgent> NearbyAgents,
    IReadOnlyList<NearbyBuilding> KnownBuildings,
    string? TownEvent = null);

/// <summary>The chosen action plus what to show in the UI (<c>AgentThought</c>).</summary>
/// <param name="FallbackReason">Why the LLM was wanted but rules were used (null if not applicable).</param>
public sealed record AgentDecision(ActionRequested Request, string Thought, int Importance, InterestingMoment? Moment, string? FallbackReason)
{
    public DecisionSource Source => Request.Source;

    public AgentThought ToThought() => new(Request.AgentId, Thought, Request.Type, Request.Source);
}

/// <summary>
/// Picks an agent's next action. Routine moments use <see cref="RuleBasedPlanner"/>; interesting moments
/// ask the LLM. Any LLM problem (budget, timeout, error, bad JSON) falls back to the rule-based action.
/// </summary>
public sealed class AgentDecisionService(
    RuleBasedPlanner planner,
    InterestingMomentDetector detector,
    LlmScheduler scheduler,
    PromptBuilder promptBuilder,
    DecisionParser parser,
    LlmMetrics metrics)
{
    public const int RuleImportance = 1;

    public async Task<AgentDecision> DecideAsync(AgentState agent, DecisionInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(input);

        var ruleAction = planner.Plan(agent, input.Hour, input.Job);
        var moment = detector.Detect(agent, new MomentInput(input.Tick, planner.IsNight(input.Hour), input.NearbyAgents.Count, input.TownEvent));

        if (moment is null)
            return FromRules(agent, ruleAction, moment: null, fallbackReason: null);

        if (!scheduler.TryReserve())
        {
            metrics.RecordFallback(nameof(LlmFailure.BudgetExceeded));
            return FromRules(agent, ruleAction, moment, nameof(LlmFailure.BudgetExceeded));
        }

        var prompt = promptBuilder.Build(BuildContext(agent, input, moment));
        var call = await scheduler.RunReservedAsync(prompt, cancellationToken);
        if (!call.Success)
            return FromRules(agent, ruleAction, moment, call.Failure.ToString());

        var knownTargets = input.KnownBuildings.Select(b => b.Id)
            .Concat(input.NearbyAgents.Select(a => a.Id.ToString()))
            .ToHashSet(StringComparer.Ordinal);

        var parsed = parser.Parse(call.Text, knownTargets);
        if (!parsed.Success)
        {
            metrics.RecordFallback("InvalidResponse");
            return FromRules(agent, ruleAction, moment, $"InvalidResponse: {parsed.Error}");
        }

        agent.LastLlmDecisionTick = input.Tick;
        var decision = parsed.Decision!;
        return new AgentDecision(DecisionParser.ToRequest(decision, agent.Id), decision.Thought, decision.Importance, moment, FallbackReason: null);
    }

    private static AgentDecision FromRules(AgentState agent, PlannedAction action, InterestingMoment? moment, string? fallbackReason) =>
        new(action.ToRequest(agent.Id, DecisionSource.Rules), action.Reason, RuleImportance, moment, fallbackReason);

    private static AgentPromptContext BuildContext(AgentState agent, DecisionInput input, InterestingMoment moment) => new()
    {
        Name = agent.Name,
        Age = agent.Age,
        Traits = agent.Personality.Traits,
        Goal = agent.Personality.Goal,
        SpeakingStyle = agent.Personality.SpeakingStyle,
        Day = input.Day,
        Hour = input.Hour,
        Minute = input.Minute,
        Money = agent.Money,
        Hunger = agent.Hunger,
        Energy = agent.Energy,
        Social = agent.Social,
        LocationId = agent.LocationId,
        JobTitle = input.Job?.JobId,
        WorkplaceId = input.Job?.BuildingId,
        HomeId = agent.HomeId,
        Inventory = agent.Inventory,
        NearbyAgents = input.NearbyAgents,
        NearbyBuildings = input.KnownBuildings,
        Situation = moment.Description,
        RecentEvents = input.TownEvent is null ? [] : [input.TownEvent],
    };
}
