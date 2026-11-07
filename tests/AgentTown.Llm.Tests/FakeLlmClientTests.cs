using AgentTown.Llm.Client;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AgentTown.Llm.Tests;

public class FakeLlmClientTests
{
    [Fact]
    public async Task Returns_queued_answers_in_order_then_default()
    {
        var llm = new FakeLlmClient().Enqueue("one", "two");
        var prompt = new LlmPrompt("s", "u");

        (await llm.CompleteAsync(prompt)).Should().Be("one");
        (await llm.CompleteAsync(prompt)).Should().Be("two");
        (await llm.CompleteAsync(prompt)).Should().Contain("\"action\":\"Idle\"");
        llm.Prompts.Should().HaveCount(3);
    }

    [Fact]
    public async Task Embeddings_are_deterministic_normalized_and_768_long()
    {
        var llm = new FakeLlmClient();

        var a1 = await llm.EmbedAsync("bread is tasty");
        var a2 = await llm.EmbedAsync("bread is tasty");
        var b = await llm.EmbedAsync("the bank collapsed");

        a1.Should().HaveCount(768).And.Equal(a2);
        b.Should().NotEqual(a1);
        MathF.Sqrt(a1.Sum(v => v * v)).Should().BeApproximately(1f, 0.001f);
    }

    [Fact]
    public async Task Complete_json_deserializes_and_strips_fences()
    {
        var llm = new FakeLlmClient().Enqueue("```json\n{\"summary\":\"A good day.\"}\n```");

        var result = await llm.CompleteJsonAsync<DaySummary>(new LlmPrompt("s", "u"));

        result.Summary.Should().Be("A good day.");
    }

    [Theory]
    [InlineData(true, HealthStatus.Healthy)]
    [InlineData(false, HealthStatus.Degraded)]
    public async Task Health_check_reports_degraded_when_llm_is_offline(bool available, HealthStatus expected)
    {
        var check = new LlmHealthCheck(new FakeLlmClient { Available = available }, new LlmOptions());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(expected);
    }

    private sealed record DaySummary(string Summary);
}
