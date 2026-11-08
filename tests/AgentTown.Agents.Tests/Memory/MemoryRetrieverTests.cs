using AgentTown.Agents.Memory;
using AgentTown.Contracts.Events;
using AgentTown.Llm.Client;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgentTown.Agents.Tests.Memory;

public sealed class MemoryRetrieverTests
{
    private const long Day = TickOccurred.TicksPerDay;
    private static readonly Guid Jonas = Guid.NewGuid();

    private readonly FakeLlmClient _llm = new();
    private readonly MemoryOptions _options = new();
    private readonly InMemoryMemoryStore _store;

    public MemoryRetrieverTests() => _store = new InMemoryMemoryStore(_options);

    private MemoryRetriever Retriever(ILlmClient? llm = null) =>
        new(_store, llm ?? _llm, _options, NullLogger<MemoryRetriever>.Instance);

    private async Task<MemoryRecord> Add(string text, int importance, long tick)
    {
        var memory = new MemoryRecord
        {
            Id = Guid.NewGuid(), AgentId = Jonas, Tick = tick, Kind = MemoryKind.Observation, Text = text,
            Importance = importance, Embedding = await _llm.EmbedAsync(text), LastAccessedTick = tick,
        };
        await _store.AddAsync(memory);
        return memory;
    }

    [Fact]
    public async Task More_important_and_more_recent_memory_ranks_higher()
    {
        var old = await Add("I bought bread at bakery-1.", importance: 2, tick: 0);
        var recent = await Add("The bank collapsed and I lost my savings.", importance: 9, tick: 2 * Day);

        var top = await Retriever().RetrieveAsync(Jonas, "what should I do today", nowTick: 2 * Day + 6);

        top.Select(s => s.Memory).Should().Equal(recent, old);
        top[0].Score.Should().BeGreaterThan(top[1].Score);
    }

    [Fact]
    public async Task Relevant_memory_wins_when_importance_and_recency_are_equal()
    {
        await Add("Ona sings in the park every evening.", importance: 4, tick: 10);
        var relevant = await Add("Petras owes me 20 EUR.", importance: 4, tick: 10);
        await Add("The factory smells of flour.", importance: 4, tick: 10);

        var top = await Retriever().RetrieveAsync(Jonas, "Petras owes me 20 EUR.", nowTick: 20, topK: 1);

        top.Should().ContainSingle().Which.Memory.Should().Be(relevant);
        top[0].Relevance.Should().Be(1);
    }

    [Fact]
    public async Task Weights_are_configurable()
    {
        var important = await Add("I got married.", importance: 10, tick: 0);
        var newest = await Add("I ate bread.", importance: 1, tick: 3 * Day);

        _options.ImportanceWeight = 0;
        _options.RelevanceWeight = 0;
        var byRecency = await Retriever().RetrieveAsync(Jonas, "anything", nowTick: 3 * Day, topK: 1);
        byRecency[0].Memory.Should().Be(newest);

        _options.ImportanceWeight = 1;
        _options.RecencyWeight = 0;
        var byImportance = await Retriever().RetrieveAsync(Jonas, "anything", nowTick: 3 * Day, topK: 1);
        byImportance[0].Memory.Should().Be(important);
    }

    [Fact]
    public async Task Returns_top_k_and_marks_them_accessed()
    {
        for (var i = 0; i < 8; i++)
            await Add($"Memory {i}", importance: i + 1, tick: i);

        var top = await Retriever().RetrieveAsync(Jonas, "Memory", nowTick: 100);

        top.Should().HaveCount(_options.TopK);
        top.Should().OnlyContain(s => s.Memory.LastAccessedTick == 100);
        (await _store.GetByAgentAsync(Jonas)).Count(m => m.LastAccessedTick == 100).Should().Be(_options.TopK);
    }

    [Fact]
    public async Task Recency_decays_per_simulation_hour()
    {
        await Add("a", 5, tick: 0);
        await Add("b", 5, tick: 6 * 24);   // 24 hours later

        var top = await Retriever().RetrieveAsync(Jonas, "", nowTick: 6 * 24);

        top[0].Memory.Text.Should().Be("b");
        top[0].Recency.Should().Be(1);
        top[1].Recency.Should().Be(0);
    }

    [Fact]
    public async Task Works_without_embeddings()
    {
        await Add("unimportant", 1, tick: 0);
        await Add("important", 8, tick: 0);

        var top = await Retriever(new FailingEmbeddingClient()).RetrieveAsync(Jonas, "query", nowTick: 10);

        top[0].Memory.Text.Should().Be("important");
        top.Should().OnlyContain(s => s.Relevance == 0);
    }

    [Fact]
    public async Task Agent_without_memories_gets_nothing()
    {
        (await Retriever().RetrieveAsync(Guid.NewGuid(), "hello", 0)).Should().BeEmpty();
    }

    [Fact]
    public async Task Prompt_lines_have_time_and_are_in_chronological_order()
    {
        await Add("I lost my job.", 8, tick: Day + 54);    // day 2, 09:00
        await Add("I found a coin.", 2, tick: 6);          // day 1, 01:00

        var lines = await Retriever().RetrieveForPromptAsync(Jonas, "job", nowTick: Day + 60);

        lines.Should().Equal("day 1, 01:00: I found a coin.", "day 2, 09:00: I lost my job.");
    }

    [Fact]
    public void Cosine_similarity()
    {
        MemoryRetriever.Cosine([1, 0], [1, 0]).Should().BeApproximately(1, 1e-9);
        MemoryRetriever.Cosine([1, 0], [0, 1]).Should().BeApproximately(0, 1e-9);
        MemoryRetriever.Cosine([1, 0], [-2, 0]).Should().BeApproximately(-1, 1e-9);
        MemoryRetriever.Cosine(null, [1, 0]).Should().Be(0);
        MemoryRetriever.Cosine([1, 0, 0], [1, 0]).Should().Be(0);
    }
}

internal sealed class FailingEmbeddingClient : ILlmClient
{
    public Task<string> CompleteAsync(LlmPrompt prompt, CancellationToken cancellationToken = default) =>
        throw new HttpRequestException("offline");

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default) =>
        throw new HttpRequestException("offline");

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
}
