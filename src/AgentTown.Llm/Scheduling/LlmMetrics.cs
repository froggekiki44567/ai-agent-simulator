using System.Diagnostics.Metrics;

namespace AgentTown.Llm.Scheduling;

/// <summary>
/// LLM metrics (shown in the Aspire dashboard): llm.calls, llm.latency, llm.fallbacks, llm.queue_length.
/// </summary>
public sealed class LlmMetrics : IDisposable
{
    public const string MeterName = "AgentTown.Llm";

    private readonly Meter _meter;
    private int _queueLength;

    public LlmMetrics(IMeterFactory? meterFactory = null)
    {
        _meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName);
        Calls = _meter.CreateCounter<long>("llm.calls", description: "LLM calls that were started.");
        Latency = _meter.CreateHistogram<double>("llm.latency", unit: "ms", description: "Time from request to answer.");
        Fallbacks = _meter.CreateCounter<long>("llm.fallbacks", description: "Decisions that fell back to rules.");
        _meter.CreateObservableGauge("llm.queue_length", () => Volatile.Read(ref _queueLength), description: "Requests waiting for a free LLM slot.");
    }

    public Counter<long> Calls { get; }
    public Histogram<double> Latency { get; }
    public Counter<long> Fallbacks { get; }

    public int QueueLength => Volatile.Read(ref _queueLength);

    internal void EnterQueue() => Interlocked.Increment(ref _queueLength);
    internal void LeaveQueue() => Interlocked.Decrement(ref _queueLength);

    /// <summary>Counts a fallback with its reason (e.g. Timeout, BudgetExceeded, InvalidResponse).</summary>
    public void RecordFallback(string reason) => Fallbacks.Add(1, new KeyValuePair<string, object?>("reason", reason));

    public void Dispose() => _meter.Dispose();
}
