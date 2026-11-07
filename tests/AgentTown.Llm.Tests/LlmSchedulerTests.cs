using System.Diagnostics.Metrics;
using AgentTown.Llm.Client;
using AgentTown.Llm.Scheduling;
using FluentAssertions;

namespace AgentTown.Llm.Tests;

public sealed class LlmSchedulerTests : IDisposable
{
    private static readonly LlmPrompt Prompt = new("system", "user");
    private readonly LlmMetrics _metrics = new();
    private readonly FakeLlmClient _llm = new();

    private LlmScheduler Create(int maxCallsPerTick = 10, int maxConcurrency = 2, double timeoutSeconds = 5) =>
        new(_llm, new LlmOptions { MaxCallsPerTick = maxCallsPerTick, MaxConcurrency = maxConcurrency, TimeoutSeconds = timeoutSeconds }, _metrics);

    [Fact]
    public async Task Returns_model_answer()
    {
        _llm.Enqueue("""{"action":"Idle"}""");
        using var scheduler = Create();

        var result = await scheduler.RequestAsync(Prompt);

        result.Success.Should().BeTrue();
        result.Text.Should().Be("""{"action":"Idle"}""");
    }

    [Fact]
    public async Task Budget_limits_calls_per_tick_and_resets_on_next_tick()
    {
        using var scheduler = Create(maxCallsPerTick: 2);
        scheduler.OnTick(1);

        (await scheduler.RequestAsync(Prompt)).Success.Should().BeTrue();
        (await scheduler.RequestAsync(Prompt)).Success.Should().BeTrue();
        (await scheduler.RequestAsync(Prompt)).Failure.Should().Be(LlmFailure.BudgetExceeded);
        _llm.Prompts.Should().HaveCount(2, "the third call never reaches the model");

        scheduler.OnTick(2);
        scheduler.RemainingBudget.Should().Be(2);
        (await scheduler.RequestAsync(Prompt)).Success.Should().BeTrue();
    }

    [Fact]
    public async Task Same_tick_twice_does_not_reset_budget()
    {
        using var scheduler = Create(maxCallsPerTick: 1);
        scheduler.OnTick(5);
        await scheduler.RequestAsync(Prompt);

        scheduler.OnTick(5);

        scheduler.RemainingBudget.Should().Be(0);
    }

    [Fact]
    public async Task Slow_model_times_out()
    {
        _llm.Delay = TimeSpan.FromSeconds(10);
        using var scheduler = Create(timeoutSeconds: 0.05);

        var result = await scheduler.RequestAsync(Prompt);

        result.Failure.Should().Be(LlmFailure.Timeout);
        result.Latency.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Model_error_is_reported_not_thrown()
    {
        using var scheduler = new LlmScheduler(new ThrowingLlmClient(), new LlmOptions(), _metrics);

        var result = await scheduler.RequestAsync(Prompt);

        result.Failure.Should().Be(LlmFailure.Error);
    }

    [Fact]
    public async Task No_more_than_max_concurrency_calls_run_at_once()
    {
        var counting = new CountingLlmClient(TimeSpan.FromMilliseconds(50));
        using var scheduler = new LlmScheduler(counting, new LlmOptions { MaxConcurrency = 2, MaxCallsPerTick = 100 }, _metrics);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => scheduler.RequestAsync(Prompt)));

        counting.MaxSeenConcurrency.Should().Be(2);
        _metrics.QueueLength.Should().Be(0);
    }

    [Fact]
    public async Task Fallbacks_are_counted_with_reason()
    {
        var fallbacks = new List<string?>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == LlmMetrics.MeterName && instrument.Name == "llm.fallbacks")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            fallbacks.Add(tags.ToArray().Single(t => t.Key == "reason").Value?.ToString()));
        listener.Start();

        using var scheduler = Create(maxCallsPerTick: 0);
        await scheduler.RequestAsync(Prompt);

        fallbacks.Should().Contain(nameof(LlmFailure.BudgetExceeded));
    }

    public void Dispose() => _metrics.Dispose();

    private sealed class ThrowingLlmClient : ILlmClient
    {
        public Task<string> CompleteAsync(LlmPrompt prompt, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("connection refused");
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default) => throw new HttpRequestException();
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class CountingLlmClient(TimeSpan delay) : ILlmClient
    {
        private int _running;
        public int MaxSeenConcurrency { get; private set; }

        public async Task<string> CompleteAsync(LlmPrompt prompt, CancellationToken cancellationToken = default)
        {
            var now = Interlocked.Increment(ref _running);
            lock (this) MaxSeenConcurrency = Math.Max(MaxSeenConcurrency, now);
            await Task.Delay(delay, cancellationToken);
            Interlocked.Decrement(ref _running);
            return "{}";
        }

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<float>());
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
