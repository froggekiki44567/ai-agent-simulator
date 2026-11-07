using AgentTown.Agents.Memory;
using AgentTown.Agents.Relationships;
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
    LlmMetrics metrics,
    MemoryRetriever? memories = null,
    RelationshipService? relationships = null)
{
    public const int RuleImportance = 1;

    public async Task<AgentDecision> DecideAsync(AgentState agent, DecisionInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(input);

        var ruleAction = planner.Plan(agent, input.Hour, input.Job);

        // Relationships are only needed to judge the people nearby, or for the prompt once the LLM is used.
        IReadOnlyList<RelationshipRecord>? known = input.NearbyAgents.Count > 0 ? await KnownAsync(agent, cancellationToken) : null;
        var moment = detector.Detect(agent, new MomentInput(input.Tick, planner.IsNight(input.Hour), input.NearbyAgents.Count, input.TownEvent,
            NearbyRelations(input.NearbyAgents, known)));

        if (moment is null)
            return FromRules(agent, ruleAction, moment: null, fallbackReason: null);

        if (!scheduler.TryReserve())
        {
            metrics.RecordFallback(nameof(LlmFailure.BudgetExceeded));
            return FromRules(agent, ruleAction, moment, nameof(LlmFailure.BudgetExceeded));
        }

        known ??= await KnownAsync(agent, cancellationToken);
        var remembered = memories is null
            ? []
            : await memories.RetrieveForPromptAsync(agent.Id, MemoryQuery(agent, input, moment), input.Tick, cancellationToken);
        var prompt = promptBuilder.Build(BuildContext(agent, input with { NearbyAgents = DescribeNearby(input.NearbyAgents, known) }, moment, remembered)
            with { KnownPeople = DescribeKnownPeople(input.NearbyAgents, known) });
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

    private async Task<IReadOnlyList<RelationshipRecord>> KnownAsync(AgentState agent, CancellationToken cancellationToken)
    {
        if (relationships is null)
            return [];
        try
        {
            return await relationships.GetForAgentAsync(agent.Id, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Deciding without relationships is better than not deciding.
            return [];
        }
    }

    private static IReadOnlyList<NearbyRelation>? NearbyRelations(IReadOnlyList<NearbyAgent> nearby, IReadOnlyList<RelationshipRecord>? known)
    {
        if (known is null || known.Count == 0)
            return null;
        var byOther = known.ToDictionary(r => r.OtherAgentId);
        return nearby
            .Where(a => byOther.ContainsKey(a.Id))
            .Select(a => new NearbyRelation(a.Name, byOther[a.Id].Affinity, byOther[a.Id].Balance))
            .ToArray();
    }

    private IReadOnlyList<NearbyAgent> DescribeNearby(IReadOnlyList<NearbyAgent> nearby, IReadOnlyList<RelationshipRecord> known)
    {
        if (relationships is null || known.Count == 0)
            return nearby;
        var byOther = known.ToDictionary(r => r.OtherAgentId);
        return nearby
            .Select(a => byOther.TryGetValue(a.Id, out var r) ? a with { Relationship = relationships.Describe(r) } : a)
            .ToArray();
    }

    /// <summary>The most notable people the agent knows who are not nearby: debts first, then the strongest feelings.</summary>
    private IReadOnlyList<string> DescribeKnownPeople(IReadOnlyList<NearbyAgent> nearby, IReadOnlyList<RelationshipRecord> known)
    {
        if (relationships is null || known.Count == 0 || relationships.Options.PromptLimit <= 0)
            return [];

        var here = nearby.Select(a => a.Id).ToHashSet();
        return known
            .Select(r => (Relationship: r, Name: relationships.NameOf(r.OtherAgentId)))
            .Where(x => !here.Contains(x.Relationship.OtherAgentId) && x.Name is not null)
            .OrderByDescending(x => Math.Abs(x.Relationship.Balance) >= relationships.Options.DebtMentionAbove)
            .ThenByDescending(x => Math.Abs(x.Relationship.Affinity))
            .ThenByDescending(x => x.Relationship.LastInteractionTick)
            .Take(relationships.Options.PromptLimit)
            .Select(x => $"{x.Name}: {relationships.Describe(x.Relationship)} (last met {RelationshipService.LastMet(x.Relationship)})")
            .ToArray();
    }

    /// <summary>What the agent is thinking about right now; memories similar to this are more relevant.</summary>
    private static string MemoryQuery(AgentState agent, DecisionInput input, InterestingMoment moment)
    {
        var parts = new List<string> { moment.Description, $"at {agent.LocationId ?? "the street"}" };
        if (input.NearbyAgents.Count > 0)
            parts.Add("with " + string.Join(", ", input.NearbyAgents.Select(a => a.Name)));
        if (input.TownEvent is not null)
            parts.Add(input.TownEvent);
        return string.Join(". ", parts);
    }

    private static AgentPromptContext BuildContext(AgentState agent, DecisionInput input, InterestingMoment moment, IReadOnlyList<string> remembered) => new()
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
        Memories = remembered,
        RecentEvents = input.TownEvent is null ? [] : [input.TownEvent],
    };
}
