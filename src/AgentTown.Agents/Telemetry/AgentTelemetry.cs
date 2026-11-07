using System.Diagnostics;
using System.Diagnostics.Metrics;
using AgentTown.Agents.Planning;
using AgentTown.Contracts.Model;

namespace AgentTown.Agents.Telemetry;

/// <summary>
/// Traces and metrics of the Agents service (visible in the Aspire dashboard):
/// one span per decision, plus the counters <c>agents.decisions</c> and <c>agents.action_failures</c>.
/// </summary>
public sealed class AgentTelemetry : IDisposable
{
    public const string SourceName = "AgentTown.Agents";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    private readonly Meter _meter;
    private readonly Counter<long> _decisions;
    private readonly Counter<long> _actionFailures;

    public AgentTelemetry(IMeterFactory? meterFactory = null)
    {
        _meter = meterFactory?.Create(SourceName) ?? new Meter(SourceName);
        _decisions = _meter.CreateCounter<long>("agents.decisions", description: "Decisions made, by source (Rules or Llm).");
        _actionFailures = _meter.CreateCounter<long>("agents.action_failures", description: "Actions rejected by World.");
    }

    /// <summary>Starts the span for one agent's decision. Dispose it when the decision is made.</summary>
    public static Activity? StartDecision(Guid agentId, string agentName, long tick)
    {
        var activity = ActivitySource.StartActivity("agent.decide");
        activity?.SetTag("agent.id", agentId);
        activity?.SetTag("agent.name", agentName);
        activity?.SetTag("town.tick", tick);
        return activity;
    }

    public void RecordDecision(Activity? activity, AgentDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        activity?.SetTag("decision.source", decision.Source.ToString());
        activity?.SetTag("decision.action", decision.Request.Type.ToString());
        activity?.SetTag("decision.target", decision.Request.TargetId);
        if (decision.Moment is not null)
            activity?.SetTag("decision.moment", decision.Moment.Kind.ToString());
        if (decision.FallbackReason is not null)
            activity?.SetTag("decision.fallback", decision.FallbackReason);

        _decisions.Add(1,
            new KeyValuePair<string, object?>("source", decision.Source.ToString()),
            new KeyValuePair<string, object?>("action", decision.Request.Type.ToString()));
    }

    public void RecordActionFailure(ActionType action, string? reason) =>
        _actionFailures.Add(1,
            new KeyValuePair<string, object?>("action", action.ToString()),
            new KeyValuePair<string, object?>("reason", reason ?? "unknown"));

    public void Dispose() => _meter.Dispose();
}
