using AgentTown.Contracts.Model;

namespace AgentTown.Agents.State;

/// <summary>How fast needs drop per tick and how much actions restore them.</summary>
public sealed class NeedsOptions
{
    public const string SectionName = "Agents:Needs";

    // With 1 tick = 10 min: Hunger 100 → 30 in about 8 h, Energy 100 → 20 in about 16 h awake.
    public double HungerDecayPerTick { get; set; } = 1.5;
    public double EnergyDecayPerTick { get; set; } = 0.8;
    public double SocialDecayPerTick { get; set; } = 0.5;

    /// <summary>Energy gained per tick while sleeping (100 in about 8 h).</summary>
    public double SleepEnergyPerTick { get; set; } = 2.1;

    public double EatHungerGain { get; set; } = 50;
    public double TalkSocialGain { get; set; } = 25;

    /// <summary>Working is tiring: extra energy lost when a Work action completes.</summary>
    public double WorkEnergyCost { get; set; } = 2;
}

/// <summary>
/// Updates Hunger, Energy and Social. Needs drop every tick and recover after actions.
/// </summary>
public sealed class NeedsSystem(NeedsOptions options)
{
    public NeedsOptions Options { get; } = options;

    /// <summary>Called once per tick for every agent.</summary>
    public void ApplyTick(AgentState agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        agent.Hunger = AgentState.ClampNeed(agent.Hunger - Options.HungerDecayPerTick);
        agent.Social = AgentState.ClampNeed(agent.Social - Options.SocialDecayPerTick);
        agent.Energy = agent.Status == AgentStatus.Sleeping
            ? AgentState.ClampNeed(agent.Energy + Options.SleepEnergyPerTick)
            : AgentState.ClampNeed(agent.Energy - Options.EnergyDecayPerTick);
    }

    /// <summary>Called when World confirms an action succeeded.</summary>
    public void ApplyActionCompleted(AgentState agent, ActionType action)
    {
        ArgumentNullException.ThrowIfNull(agent);

        switch (action)
        {
            case ActionType.Eat:
                agent.Hunger = AgentState.ClampNeed(agent.Hunger + Options.EatHungerGain);
                break;
            case ActionType.Talk:
                agent.Social = AgentState.ClampNeed(agent.Social + Options.TalkSocialGain);
                break;
            case ActionType.Work:
                agent.Energy = AgentState.ClampNeed(agent.Energy - Options.WorkEnergyCost);
                break;
            case ActionType.Sleep:
                agent.Status = AgentStatus.Sleeping;
                break;
        }
    }
}
