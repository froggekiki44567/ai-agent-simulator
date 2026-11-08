using AgentTown.Agents.Loop;
using AgentTown.Contracts.Model;
using AgentTown.Llm.Client;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AgentTown.Agents.Tests.Loop;

public class WiringTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection().AddLogging().AddAgentServices(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void All_services_resolve_without_ollama()
    {
        using var provider = Build([]);

        provider.GetRequiredService<AgentLoop>().Should().NotBeNull();
        provider.GetRequiredService<AgentTown.Contracts.Mind.IAgentMindQuery>().Should().NotBeNull();
        provider.GetRequiredService<ILlmClient>().Should().BeOfType<FakeLlmClient>();
    }

    [Fact]
    public void Uses_ollama_when_connection_string_is_set()
    {
        using var provider = Build(new() { ["ConnectionStrings:ollama"] = "Endpoint=http://host.docker.internal:11434" });

        provider.GetRequiredService<ILlmClient>().Should().BeOfType<OllamaLlmClient>();
    }

    [Fact]
    public void Options_are_read_from_configuration()
    {
        using var provider = Build(new()
        {
            ["Llm:Model"] = "qwen2.5:3b-instruct",
            ["Llm:MaxCallsPerTick"] = "7",
            ["Agents:Planner:HungryBelow"] = "45",
        });

        provider.GetRequiredService<AgentTown.Llm.LlmOptions>().Should().BeEquivalentTo(new { Model = "qwen2.5:3b-instruct", MaxCallsPerTick = 7 });
        provider.GetRequiredService<AgentTown.Agents.Planning.PlannerOptions>().HungryBelow.Should().Be(45);
    }

    [Theory]
    [InlineData("Endpoint=http://host.docker.internal:11434", "http://host.docker.internal:11434/")]
    [InlineData("endpoint = http://10.0.0.5:11434 ; Model=x", "http://10.0.0.5:11434/")]
    [InlineData("http://localhost:11434", "http://localhost:11434/")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("not a url", null)]
    public void Parses_ollama_connection_string(string? connectionString, string? expected)
    {
        AgentServiceCollectionExtensions.ParseEndpoint(connectionString)?.ToString().Should().Be(expected);
        if (expected is null)
            AgentServiceCollectionExtensions.ParseEndpoint(connectionString).Should().BeNull();
    }

    [Fact]
    public void Inventory_changes_follow_confirmed_actions()
    {
        var agent = TestAgents.Create();

        agent.ApplyInventoryChange(ActionType.Buy, "bread", 3);
        agent.ApplyInventoryChange(ActionType.Eat, "bread", 1);
        agent.ApplyInventoryChange(ActionType.Move, "bread", 5);
        agent.ItemCount("bread").Should().Be(2);

        agent.ApplyInventoryChange(ActionType.Sell, "bread", 10);
        agent.Inventory.Should().NotContainKey("bread");
    }
}
