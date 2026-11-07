using AgentTown.Agents.State;
using AgentTown.Contracts.Model;

namespace AgentTown.Agents.Planning;

public sealed class PlannerOptions
{
    public const string SectionName = "Agents:Planner";

    public int SleepStartHour { get; set; } = 23;
    public int SleepEndHour { get; set; } = 7;

    public double HungryBelow { get; set; } = 30;
    public double TiredBelow { get; set; } = 20;
    public double LonelyBelow { get; set; } = 40;

    /// <summary>Where agents buy food.</summary>
    public string FoodBuildingId { get; set; } = "bakery-1";
    public string FoodItemId { get; set; } = "bread";
    public decimal FoodPriceEstimate { get; set; } = 3m;

    /// <summary>Where lonely agents go to meet people.</summary>
    public string SocialBuildingId { get; set; } = "park-1";
}

/// <summary>
/// Routine behavior without the LLM (ADR-003). Picks one action from simple rules, in priority order:
/// sleep at night → eat when hungry → rest when tired → work during the shift → meet people when lonely → idle.
/// Multi-step goals (e.g. "buy bread") are split into one step per call: first Move, then Buy, then Eat.
/// </summary>
public sealed class RuleBasedPlanner(PlannerOptions options)
{
    public PlannerOptions Options { get; } = options;

    public PlannedAction Plan(AgentState agent, int hour, JobInfo? job)
    {
        ArgumentNullException.ThrowIfNull(agent);
        if (hour is < 0 or > 23)
            throw new ArgumentOutOfRangeException(nameof(hour));

        // A sleeping agent keeps sleeping through the night or until rested.
        // In the morning it simply plans its day; World sets it Active again on the next action.
        if (agent.Status == AgentStatus.Sleeping && (IsNight(hour) || agent.Energy < Options.TiredBelow))
            return PlannedAction.Idle("Sleeping.");

        if (IsNight(hour))
            return GoHomeAndSleep(agent, "It's late, time to sleep.");

        if (agent.Hunger < Options.HungryBelow && PlanEating(agent) is { } eat)
            return eat;

        if (agent.Energy < Options.TiredBelow)
            return GoHomeAndSleep(agent, "I'm exhausted and need to rest.");

        if (job is not null && job.IsOnShift(hour))
        {
            return agent.IsAt(job.BuildingId)
                ? new PlannedAction(ActionType.Work, "It's my shift.", job.BuildingId)
                : MoveTo(job.BuildingId, "Going to work.");
        }

        if (agent.Social < Options.LonelyBelow && !agent.IsAt(Options.SocialBuildingId))
            return MoveTo(Options.SocialBuildingId, "I feel lonely, going to see people.");

        if (job is null && agent.Status == AgentStatus.Bankrupt)
            return PlannedAction.Idle("No job and no money, waiting for an opportunity.");

        return PlannedAction.Idle("Nothing to do right now.");
    }

    public bool IsNight(int hour) =>
        Options.SleepStartHour > Options.SleepEndHour
            ? hour >= Options.SleepStartHour || hour < Options.SleepEndHour
            : hour >= Options.SleepStartHour && hour < Options.SleepEndHour;

    /// <summary>Returns <c>null</c> if the agent can't get food (no food and no money).</summary>
    private PlannedAction? PlanEating(AgentState agent)
    {
        if (agent.ItemCount(Options.FoodItemId) > 0)
            return new PlannedAction(ActionType.Eat, "I'm hungry, eating what I have.", ItemId: Options.FoodItemId, Quantity: 1);

        if (agent.Status == AgentStatus.Bankrupt || agent.Money < Options.FoodPriceEstimate)
            return null;

        return agent.IsAt(Options.FoodBuildingId)
            ? new PlannedAction(ActionType.Buy, "I'm hungry, buying food.", Options.FoodBuildingId, Options.FoodItemId, Quantity: 1)
            : MoveTo(Options.FoodBuildingId, "I'm hungry, going to buy food.");
    }

    private static PlannedAction GoHomeAndSleep(AgentState agent, string reason) =>
        agent.IsAt(agent.HomeId)
            ? new PlannedAction(ActionType.Sleep, reason, agent.HomeId)
            : MoveTo(agent.HomeId, reason + " Going home.");

    private static PlannedAction MoveTo(string buildingId, string reason) =>
        new(ActionType.Move, reason, buildingId);
}
