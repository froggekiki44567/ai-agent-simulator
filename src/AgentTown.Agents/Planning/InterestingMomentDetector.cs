using AgentTown.Agents.State;
using AgentTown.Contracts.Model;

namespace AgentTown.Agents.Planning;

public enum MomentKind
{
    /// <summary>A town event affects this agent. Always uses the LLM (ignores the cooldown).</summary>
    TownEvent,

    /// <summary>World rejected the agent's last action.</summary>
    ActionFailed,

    /// <summary>Someone is nearby and the agent wants company.</summary>
    SocialChance,

    /// <summary>The agent is running out of money.</summary>
    LowMoney,
}

/// <summary>Why this moment is worth an LLM call. <see cref="Description"/> goes into the prompt.</summary>
public sealed record InterestingMoment(MomentKind Kind, string Description);

public sealed class InterestingMomentOptions
{
    public const string SectionName = "Agents:InterestingMoments";

    /// <summary>Minimum ticks between two LLM decisions of the same agent (6 = 1 simulation hour).</summary>
    public int CooldownTicks { get; set; } = 6;

    public decimal LowMoneyBelow { get; set; } = 20m;

    /// <summary>Talk to nearby people when Social is below this.</summary>
    public double SocialChanceBelow { get; set; } = 50;
}

/// <summary>What the detector knows about the moment besides the agent itself.</summary>
/// <param name="TownEvent">Description of a town event that affects the agent, if any.</param>
public sealed record MomentInput(long Tick, bool IsNight, int NearbyAgentCount, string? TownEvent = null);

/// <summary>
/// Decides when an agent gets an LLM decision instead of a rule-based one (ADR-003).
/// Returns <c>null</c> for routine moments, which the rule-based planner handles.
/// </summary>
public sealed class InterestingMomentDetector(InterestingMomentOptions options)
{
    public InterestingMomentOptions Options { get; } = options;

    public InterestingMoment? Detect(AgentState agent, MomentInput input)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(input);

        if (input.TownEvent is not null)
            return new(MomentKind.TownEvent, $"Something happened in town: {input.TownEvent}");

        if (agent.Status == AgentStatus.Sleeping || input.IsNight)
            return null;

        if (agent.LastLlmDecisionTick is { } last && input.Tick - last < Options.CooldownTicks)
            return null;

        if (agent.LastActionFailed)
            return new(MomentKind.ActionFailed, "Your last action did not work out. Think of something else.");

        if (input.NearbyAgentCount > 0 && agent.Social < Options.SocialChanceBelow)
            return new(MomentKind.SocialChance, "There are people nearby and you would like some company.");

        if (agent.Money < Options.LowMoneyBelow)
            return new(MomentKind.LowMoney, $"You only have {agent.Money:0.00} EUR left. What will you do about it?");

        return null;
    }
}
