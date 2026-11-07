using System.Diagnostics;
using AgentTown.Llm.Client;

namespace AgentTown.Llm.Scheduling;

public enum LlmFailure
{
    /// <summary>The per-tick budget (<see cref="LlmOptions.MaxCallsPerTick"/>) was used up.</summary>
    BudgetExceeded,

    /// <summary>No answer within <see cref="LlmOptions.TimeoutSeconds"/> (waiting in the queue included).</summary>
    Timeout,

    /// <summary>The model server failed (offline, HTTP error, ...).</summary>
    Error,
}

public sealed record LlmCallResult(string? Text, LlmFailure? Failure, TimeSpan Latency)
{
    public bool Success => Failure is null;
}

/// <summary>
/// Protects the LLM from overload (ADR-003): at most <see cref="LlmOptions.MaxConcurrency"/> calls run at once,
/// at most <see cref="LlmOptions.MaxCallsPerTick"/> start per tick, and each call has a timeout.
/// It never throws for these cases; it returns a failed <see cref="LlmCallResult"/> so the caller uses rules instead.
/// </summary>
public sealed class LlmScheduler : IDisposable
{
    private readonly ILlmClient _client;
    private readonly LlmOptions _options;
    private readonly LlmMetrics _metrics;
    private readonly SemaphoreSlim _slots;
    private long _currentTick = long.MinValue;
    private int _callsThisTick;
    private readonly Lock _budgetLock = new();

    public LlmScheduler(ILlmClient client, LlmOptions options, LlmMetrics metrics)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxConcurrency, 1);
        _client = client;
        _options = options;
        _metrics = metrics;
        _slots = new SemaphoreSlim(options.MaxConcurrency, options.MaxConcurrency);
    }

    /// <summary>Calls left in the current tick.</summary>
    public int RemainingBudget
    {
        get { lock (_budgetLock) return Math.Max(0, _options.MaxCallsPerTick - _callsThisTick); }
    }

    /// <summary>Call on every <c>TickOccurred</c>; resets the per-tick budget.</summary>
    public void OnTick(long tick)
    {
        lock (_budgetLock)
        {
            if (tick == _currentTick)
                return;
            _currentTick = tick;
            _callsThisTick = 0;
        }
    }

    /// <summary>
    /// Takes one call from this tick's budget. Use it to decide <i>before</i> building a prompt
    /// whether the LLM can be used at all.
    /// </summary>
    public bool TryReserve()
    {
        lock (_budgetLock)
        {
            if (_callsThisTick >= _options.MaxCallsPerTick)
                return false;
            _callsThisTick++;
            return true;
        }
    }

    /// <summary>Reserves budget and calls the LLM.</summary>
    public Task<LlmCallResult> RequestAsync(LlmPrompt prompt, CancellationToken cancellationToken = default)
    {
        if (!TryReserve())
        {
            _metrics.RecordFallback(nameof(LlmFailure.BudgetExceeded));
            return Task.FromResult(new LlmCallResult(null, LlmFailure.BudgetExceeded, TimeSpan.Zero));
        }

        return RunReservedAsync(prompt, cancellationToken);
    }

    /// <summary>Calls the LLM using budget already taken with <see cref="TryReserve"/>.</summary>
    public async Task<LlmCallResult> RunReservedAsync(LlmPrompt prompt, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        _metrics.EnterQueue();
        var gotSlot = false;
        try
        {
            await _slots.WaitAsync(timeout.Token);
            gotSlot = true;
            _metrics.LeaveQueue();

            _metrics.Calls.Add(1);
            var text = await _client.CompleteAsync(prompt, timeout.Token);
            _metrics.Latency.Record(stopwatch.Elapsed.TotalMilliseconds);
            return new LlmCallResult(text, null, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail(LlmFailure.Timeout, stopwatch.Elapsed);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException)
        {
            return Fail(LlmFailure.Error, stopwatch.Elapsed);
        }
        finally
        {
            if (gotSlot)
                _slots.Release();
            else
                _metrics.LeaveQueue();
        }
    }

    private LlmCallResult Fail(LlmFailure failure, TimeSpan elapsed)
    {
        _metrics.RecordFallback(failure.ToString());
        return new LlmCallResult(null, failure, elapsed);
    }

    public void Dispose() => _slots.Dispose();
}
