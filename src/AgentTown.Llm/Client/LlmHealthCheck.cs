using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AgentTown.Llm.Client;

/// <summary>
/// Reports whether the LLM is reachable. "Degraded", not "Unhealthy", when it isn't:
/// the town keeps running on rules (ADR-003).
/// </summary>
public sealed class LlmHealthCheck(ILlmClient client, LlmOptions options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return await client.IsAvailableAsync(cancellationToken)
            ? HealthCheckResult.Healthy($"LLM ready ({options.Model}, {options.EmbeddingModel}).")
            : HealthCheckResult.Degraded($"LLM offline or models missing ({options.Model}, {options.EmbeddingModel}). Agents use rules only.");
    }
}
