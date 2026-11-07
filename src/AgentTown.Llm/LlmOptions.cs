namespace AgentTown.Llm;

/// <summary>LLM settings, bound from the <c>Llm</c> configuration section.</summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>Chat model, e.g. <c>qwen2.5:7b-instruct</c> or <c>qwen2.5:3b-instruct</c> on a weaker machine.</summary>
    public string Model { get; set; } = "qwen2.5:7b-instruct";

    /// <summary>Embedding model (768 dimensions, matches the <c>vector(768)</c> column).</summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>A decision that takes longer than this falls back to rules.</summary>
    public double TimeoutSeconds { get; set; } = 15;

    public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);

    /// <summary>How many LLM calls may run at the same time.</summary>
    public int MaxConcurrency { get; set; } = 2;

    /// <summary>How many LLM calls may start per simulation tick.</summary>
    public int MaxCallsPerTick { get; set; } = 4;

    /// <summary>Sampling temperature. Higher = more varied decisions.</summary>
    public float Temperature { get; set; } = 0.7f;
}
