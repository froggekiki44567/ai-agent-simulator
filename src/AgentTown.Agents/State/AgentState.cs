using AgentTown.Contracts.Model;

namespace AgentTown.Agents.State;

/// <summary>
/// The Agents service's local copy of one agent. World owns the real state (ADR-002);
/// this copy is updated from <see cref="AgentStateDelta"/> in every <c>ActionResult</c>.
/// Needs are 0–100, where 100 means fully satisfied (full, rested, socially happy).
/// </summary>
public sealed class AgentState
{
    public const double MinNeed = 0;
    public const double MaxNeed = 100;

    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public int Age { get; init; }
    public required AgentPersonality Personality { get; init; }
    public string? JobId { get; init; }
    public required string HomeId { get; init; }

    public decimal Money { get; set; }
    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>Building the agent is inside, or <c>null</c> when on the road.</summary>
    public string? LocationId { get; set; }

    public double Hunger { get; set; } = MaxNeed;
    public double Energy { get; set; } = MaxNeed;
    public double Social { get; set; } = MaxNeed;
    public AgentStatus Status { get; set; } = AgentStatus.Active;

    /// <summary>Items the agent carries (item id → quantity), e.g. "bread" → 2.</summary>
    public Dictionary<string, int> Inventory { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The action sent to World that has no result yet. An agent has at most one at a time.
    /// </summary>
    public PendingAction? PendingAction { get; set; }

    public bool IsBusy => PendingAction is not null;

    /// <summary>Tick of the last decision made by the LLM, used for the LLM cooldown.</summary>
    public long? LastLlmDecisionTick { get; set; }

    /// <summary>True if World rejected the agent's last action.</summary>
    public bool LastActionFailed { get; set; }

    public bool IsAt(string? buildingId) =>
        buildingId is not null && string.Equals(LocationId, buildingId, StringComparison.Ordinal);

    public int ItemCount(string itemId) => Inventory.GetValueOrDefault(itemId);

    /// <summary>Copies every non-null field of the delta into this state.</summary>
    public void Apply(AgentStateDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        if (delta.Money is { } money) Money = money;
        if (delta.X is { } x) X = x;
        if (delta.Y is { } y) Y = y;
        if (delta.LocationId is not null) LocationId = delta.LocationId;
        if (delta.Hunger is { } hunger) Hunger = ClampNeed(hunger);
        if (delta.Energy is { } energy) Energy = ClampNeed(energy);
        if (delta.Social is { } social) Social = ClampNeed(social);
        if (delta.Status is { } status) Status = status;
    }

    public static double ClampNeed(double value) => Math.Clamp(value, MinNeed, MaxNeed);
}

public sealed record AgentPersonality(IReadOnlyList<string> Traits, string Goal, string SpeakingStyle);

/// <summary>An action that was sent to World and is waiting for its result.</summary>
public sealed record PendingAction(Guid RequestId, ActionType Type, string? TargetId, long RequestedAtTick);
