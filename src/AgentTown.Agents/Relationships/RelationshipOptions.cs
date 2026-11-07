namespace AgentTown.Agents.Relationships;

public sealed class RelationshipOptions
{
    public const string SectionName = "Agents:Relationships";

    /// <summary>Affinity change per conversation = average tone (-5…5) × this. 2 → at most ±10 per conversation.</summary>
    public double ToneMultiplier { get; set; } = 2;

    /// <summary>Receiving a transfer raises affinity towards the giver by 1 per this many EUR (at least 1).</summary>
    public decimal TransferEurPerAffinity { get; set; } = 10m;

    /// <summary>Upper limit for the affinity gain from one transfer.</summary>
    public int MaxTransferAffinity { get; set; } = 10;

    /// <summary>Debts below this are not mentioned in prompts and don't count as a reason to talk.</summary>
    public decimal DebtMentionAbove { get; set; } = 1m;

    /// <summary>How many known people (besides those nearby) go into a decision prompt.</summary>
    public int PromptLimit { get; set; } = 5;
}
