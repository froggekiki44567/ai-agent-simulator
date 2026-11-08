namespace AgentTown.Llm.Prompts;

/// <summary>Everything the LLM is told about one agent's situation.</summary>
public sealed record AgentPromptContext
{
    public required string Name { get; init; }
    public required int Age { get; init; }
    public required IReadOnlyList<string> Traits { get; init; }
    public required string Goal { get; init; }
    public required string SpeakingStyle { get; init; }

    public required int Day { get; init; }
    public required int Hour { get; init; }
    public required int Minute { get; init; }

    public required decimal Money { get; init; }
    public required double Hunger { get; init; }
    public required double Energy { get; init; }
    public required double Social { get; init; }

    /// <summary>Building the agent is in, or <c>null</c> when outside.</summary>
    public string? LocationId { get; init; }
    public string? JobTitle { get; init; }
    public string? WorkplaceId { get; init; }
    public required string HomeId { get; init; }

    public IReadOnlyDictionary<string, int> Inventory { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<NearbyAgent> NearbyAgents { get; init; } = [];
    public IReadOnlyList<NearbyBuilding> NearbyBuildings { get; init; } = [];

    /// <summary>Why the LLM is being asked now (from the interesting-moment detector).</summary>
    public string? Situation { get; init; }

    /// <summary>Most relevant memories, newest first (filled in from Day 15).</summary>
    public IReadOnlyList<string> Memories { get; init; } = [];

    /// <summary>Recent town events (filled in from Day 19).</summary>
    public IReadOnlyList<string> RecentEvents { get; init; } = [];
}

/// <param name="Relationship">Short description, e.g. "good friend" (filled in from Day 16).</param>
public sealed record NearbyAgent(Guid Id, string Name, string? Relationship = null);

public sealed record NearbyBuilding(string Id, string Type, bool IsOpen);
