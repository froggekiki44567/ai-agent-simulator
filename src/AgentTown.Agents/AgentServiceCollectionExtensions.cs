using AgentTown.Agents.Conversations;
using AgentTown.Agents.Loop;
using AgentTown.Agents.Memory;
using AgentTown.Agents.Mind;
using AgentTown.Agents.Planning;
using AgentTown.Agents.Relationships;
using AgentTown.Agents.State;
using AgentTown.Agents.Telemetry;
using AgentTown.Contracts.Mind;
using AgentTown.Llm;
using AgentTown.Llm.Client;
using AgentTown.Llm.Conversations;
using AgentTown.Llm.Decisions;
using AgentTown.Llm.Prompts;
using AgentTown.Llm.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;

namespace AgentTown.Agents;

public static class AgentServiceCollectionExtensions
{
    /// <summary>
    /// Registers the agent brain: needs, planner, LLM, decision service and the agent loop.
    /// Options come from configuration (<c>Agents:*</c>, <c>Llm</c>); Ollama from <c>ConnectionStrings:ollama</c>.
    /// </summary>
    public static IServiceCollection AddAgentServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(Bind<NeedsOptions>(configuration, NeedsOptions.SectionName));
        services.AddSingleton(Bind<PlannerOptions>(configuration, PlannerOptions.SectionName));
        services.AddSingleton(Bind<InterestingMomentOptions>(configuration, InterestingMomentOptions.SectionName));
        services.AddSingleton(Bind<AgentLoopOptions>(configuration, AgentLoopOptions.SectionName));
        services.AddSingleton(Bind<ConversationOptions>(configuration, ConversationOptions.SectionName));
        services.AddSingleton(Bind<MemoryOptions>(configuration, MemoryOptions.SectionName));
        services.AddSingleton(Bind<RelationshipOptions>(configuration, RelationshipOptions.SectionName));
        services.AddSingleton(Bind<LlmOptions>(configuration, LlmOptions.SectionName));

        services.AddSingleton<NeedsSystem>();
        services.AddSingleton<RuleBasedPlanner>();
        services.AddSingleton<InterestingMomentDetector>();
        services.AddSingleton<PromptBuilder>();
        services.AddSingleton<DecisionParser>();
        services.AddSingleton<LlmMetrics>();
        services.AddSingleton<LlmScheduler>();
        services.AddSingleton<AgentDecisionService>();
        services.AddSingleton<ConversationPromptBuilder>();
        services.AddSingleton<ConversationReplyParser>();
        services.AddSingleton<ConversationService>();
        // In-memory until the Memories table (pgvector) exists in Persistence.
        services.AddSingleton<IMemoryStore, InMemoryMemoryStore>();
        services.AddSingleton<MemoryWriter>();
        services.AddSingleton<MemoryRetriever>();
        // In-memory until the Relationships table exists in Persistence.
        services.AddSingleton<IRelationshipStore, InMemoryRelationshipStore>();
        services.AddSingleton<RelationshipService>();
        services.AddSingleton<IAgentMindQuery, AgentMindQuery>();
        services.AddSingleton<AgentTelemetry>();
        services.AddSingleton<AgentRegistry>();
        services.AddSingleton<TownDirectory>();
        services.AddSingleton<AgentLoop>();

        // Replaced by the NATS publisher once B's Messaging exists.
        services.AddSingleton<IAgentEventPublisher, LoggingAgentEventPublisher>();

        var ollamaEndpoint = ParseEndpoint(configuration.GetConnectionString("ollama"));
        if (ollamaEndpoint is null)
        {
            // No Ollama configured: the fake answers Idle, so everything still runs (mostly on rules).
            services.AddSingleton<ILlmClient, FakeLlmClient>();
        }
        else
        {
            services.AddSingleton<IOllamaApiClient>(_ => new OllamaApiClient(ollamaEndpoint));
            services.AddSingleton<ILlmClient, OllamaLlmClient>();
        }

        services.AddSingleton<LlmHealthCheck>();
        return services;
    }

    /// <summary>
    /// Reads the Ollama URL from an Aspire-style connection string (<c>Endpoint=http://host:11434</c>)
    /// or a plain URL. Returns <c>null</c> if there is none.
    /// </summary>
    public static Uri? ParseEndpoint(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        var value = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2, StringSplitOptions.TrimEntries))
            .FirstOrDefault(kv => kv.Length == 2 && kv[0].Equals("Endpoint", StringComparison.OrdinalIgnoreCase))?[1]
            ?? connectionString.Trim();

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
    }

    private static T Bind<T>(IConfiguration configuration, string section) where T : new()
    {
        var options = new T();
        configuration.GetSection(section).Bind(options);
        return options;
    }
}
