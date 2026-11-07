namespace AgentTown.Agents.Memory;

/// <summary>Kinds from the <c>Memories</c> table in PROJECT_PLAN 2.5.</summary>
public enum MemoryKind
{
    Observation,
    Conversation,
    Reflection,
    Event,
}

/// <summary>
/// One thing an agent remembers. Matches the planned <c>Memories</c> table
/// (Id, AgentId, Tick, Text, Importance, Kind, Embedding <c>vector(768)</c>).
/// </summary>
public sealed class MemoryRecord
{
    public required Guid Id { get; init; }
    public required Guid AgentId { get; init; }
    public required long Tick { get; init; }
    public required MemoryKind Kind { get; init; }
    public required string Text { get; init; }

    /// <summary>1 (forgettable) to 10 (life-changing).</summary>
    public required int Importance { get; init; }

    /// <summary>Embedding of <see cref="Text"/>, or null if embedding failed.</summary>
    public float[]? Embedding { get; init; }

    /// <summary>When the memory was last created or retrieved. Recency is measured from here (as in the Stanford paper).</summary>
    public long LastAccessedTick { get; set; }
}
