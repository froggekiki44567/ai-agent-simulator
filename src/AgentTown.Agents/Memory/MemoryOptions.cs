namespace AgentTown.Agents.Memory;

public sealed class MemoryOptions
{
    public const string SectionName = "Agents:Memory";

    /// <summary>How many memories go into a prompt.</summary>
    public int TopK { get; set; } = 5;

    public double RecencyWeight { get; set; } = 1;
    public double ImportanceWeight { get; set; } = 1;
    public double RelevanceWeight { get; set; } = 1;

    /// <summary>Recency = this ^ (simulation hours since last access). 0.995 is the value from the Stanford paper.</summary>
    public double RecencyDecayPerHour { get; set; } = 0.995;

    /// <summary>The in-memory store forgets the least important, oldest memories above this.</summary>
    public int MaxMemoriesPerAgent { get; set; } = 500;

    public double EmbeddingTimeoutSeconds { get; set; } = 5;

    public TimeSpan EmbeddingTimeout => TimeSpan.FromSeconds(EmbeddingTimeoutSeconds);
}
