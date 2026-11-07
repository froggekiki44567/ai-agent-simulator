using AgentTown.Llm.Client;
using Microsoft.Extensions.Logging;

namespace AgentTown.Agents.Memory;

/// <summary>A retrieved memory with its score parts (each 0–1 before weighting).</summary>
public sealed record ScoredMemory(MemoryRecord Memory, double Score, double Recency, double Importance, double Relevance);

/// <summary>
/// Finds the most useful memories for a situation, as in the Stanford "Generative Agents" paper:
/// score = w_recency·recency + w_importance·importance + w_relevance·relevance.
/// Recency decays exponentially per simulation hour since last access, importance is /10, and relevance is the
/// cosine similarity between the memory and the query embedding. Each part is min-max normalized over the
/// agent's memories, so the weights stay comparable. Retrieved memories count as accessed.
/// Never throws: returns an empty list on any problem.
/// </summary>
public sealed class MemoryRetriever(IMemoryStore store, ILlmClient llm, MemoryOptions options, ILogger<MemoryRetriever> logger)
{
    public async Task<IReadOnlyList<ScoredMemory>> RetrieveAsync(Guid agentId, string query, long nowTick, int? topK = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var memories = await store.GetByAgentAsync(agentId, cancellationToken);
            var k = topK ?? options.TopK;
            if (memories.Count == 0 || k <= 0)
                return [];

            var queryEmbedding = await TryEmbedAsync(query, cancellationToken);

            var recency = Normalize(memories.Select(m => Math.Pow(options.RecencyDecayPerHour, SimulationTime.HoursBetween(m.LastAccessedTick, nowTick))).ToArray());
            var importance = Normalize(memories.Select(m => m.Importance / 10.0).ToArray());
            var relevance = Normalize(memories.Select(m => Cosine(queryEmbedding, m.Embedding)).ToArray());

            var top = memories
                .Select((memory, i) => new ScoredMemory(
                    memory,
                    options.RecencyWeight * recency[i] + options.ImportanceWeight * importance[i] + options.RelevanceWeight * relevance[i],
                    recency[i], importance[i], relevance[i]))
                .OrderByDescending(s => s.Score)
                .ThenByDescending(s => s.Memory.Tick)
                .Take(k)
                .ToArray();

            await store.MarkAccessedAsync(top.Select(s => s.Memory.Id), nowTick, cancellationToken);
            return top;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Memory retrieval failed for {AgentId}.", agentId);
            return [];
        }
    }

    /// <summary>The top memories as prompt lines: "day 1, 09:00: I bought bread at bakery-1.".</summary>
    public async Task<IReadOnlyList<string>> RetrieveForPromptAsync(Guid agentId, string query, long nowTick, CancellationToken cancellationToken = default)
    {
        var top = await RetrieveAsync(agentId, query, nowTick, cancellationToken: cancellationToken);
        return top.OrderBy(s => s.Memory.Tick).Select(s => $"{SimulationTime.Describe(s.Memory.Tick)}: {s.Memory.Text}").ToArray();
    }

    /// <summary>Cosine similarity, or 0 if either vector is missing or they don't match.</summary>
    public static double Cosine(float[]? a, float[]? b)
    {
        if (a is null || b is null || a.Length == 0 || a.Length != b.Length)
            return 0;

        double dot = 0, lengthA = 0, lengthB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            lengthA += a[i] * a[i];
            lengthB += b[i] * b[i];
        }

        return lengthA == 0 || lengthB == 0 ? 0 : dot / (Math.Sqrt(lengthA) * Math.Sqrt(lengthB));
    }

    /// <summary>Min-max to 0–1. If all values are equal, the part can't rank anything, so it is 0 for all.</summary>
    private static double[] Normalize(double[] values)
    {
        var min = values.Min();
        var range = values.Max() - min;
        return range < 1e-12 ? new double[values.Length] : values.Select(v => (v - min) / range).ToArray();
    }

    private async Task<float[]?> TryEmbedAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.EmbeddingTimeout);
        try
        {
            return await llm.EmbedAsync(query, timeout.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Query embedding failed; ranking without relevance.");
            return null;
        }
    }
}
