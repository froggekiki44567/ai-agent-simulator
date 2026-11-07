using System.Text.Json;

namespace AgentTown.Agents.Seed;

/// <summary>Reads and validates <c>seed/agents.json</c>.</summary>
public static class AgentSeedLoader
{
    public const int MinTraits = 3;
    public const int MaxTraits = 5;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<IReadOnlyList<AgentSeed>> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        return await LoadAsync(stream, cancellationToken);
    }

    public static async Task<IReadOnlyList<AgentSeed>> LoadAsync(Stream json, CancellationToken cancellationToken = default)
    {
        var agents = await JsonSerializer.DeserializeAsync<List<AgentSeed>>(json, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Agent seed file is empty.");

        Validate(agents);
        return agents;
    }

    /// <summary>Throws <see cref="InvalidDataException"/> listing every problem found.</summary>
    public static void Validate(IReadOnlyList<AgentSeed> agents)
    {
        var errors = new List<string>();

        foreach (var agent in agents)
        {
            var label = string.IsNullOrWhiteSpace(agent.Name) ? agent.Id.ToString() : agent.Name;

            if (agent.Id == Guid.Empty)
                errors.Add($"{label}: id is missing.");
            if (string.IsNullOrWhiteSpace(agent.Name))
                errors.Add($"{label}: name is missing.");
            if (agent.Age is < 16 or > 100)
                errors.Add($"{label}: age {agent.Age} is outside 16–100.");
            if (agent.Traits is null || agent.Traits.Count is < MinTraits or > MaxTraits)
                errors.Add($"{label}: needs {MinTraits}–{MaxTraits} traits.");
            if (string.IsNullOrWhiteSpace(agent.Goal))
                errors.Add($"{label}: goal is missing.");
            if (string.IsNullOrWhiteSpace(agent.HomeId))
                errors.Add($"{label}: homeId is missing.");
            if (agent.Money < 0)
                errors.Add($"{label}: money cannot be negative.");
        }

        foreach (var duplicate in agents.GroupBy(a => a.Id).Where(g => g.Count() > 1))
            errors.Add($"Duplicate id {duplicate.Key}.");

        foreach (var duplicate in agents.GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            errors.Add($"Duplicate name '{duplicate.Key}'.");

        if (errors.Count > 0)
            throw new InvalidDataException("Invalid agent seed:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }
}
