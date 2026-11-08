using AgentTown.Agents.Conversations;
using AgentTown.Agents.Loop;
using AgentTown.Agents.Planning;
using AgentTown.Agents.State;
using AgentTown.Agents.Telemetry;
using AgentTown.Contracts.Events;
using AgentTown.Llm;
using AgentTown.Llm.Client;
using AgentTown.Llm.Conversations;
using AgentTown.Llm.Decisions;
using AgentTown.Llm.Prompts;
using AgentTown.Llm.Scheduling;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentTown.Agents.Tests.Loop;

/// <summary>Builds a real <see cref="AgentLoop"/> with a fake LLM and a recording publisher.</summary>
internal sealed class LoopHarness : IDisposable
{
    public FakeLlmClient Llm { get; } = new();
    public RecordingPublisher Publisher { get; } = new();
    public AgentRegistry Registry { get; } = new();
    public TownDirectory Town { get; } = new();
    public NeedsSystem Needs { get; } = new(new NeedsOptions());
    public LlmMetrics LlmMetrics { get; } = new();
    public AgentTelemetry Telemetry { get; } = new();
    public LlmScheduler Scheduler { get; }
    public ConversationService Conversations { get; }
    public AgentLoop Loop { get; }

    public LoopHarness(int maxLlmCallsPerTick = 4, int pendingTimeoutTicks = 80, InterestingMomentOptions? moments = null, ConversationOptions? conversations = null)
    {
        Town.AddBuilding(new("home-1", "Home", 0, 0))
            .AddBuilding(new("home-2", "Home", 0, 0))
            .AddBuilding(new("bakery-1", "Bakery", 6, 20))
            .AddBuilding(new("park-1", "Park", 0, 0))
            .AddJob(new JobInfo("job-baker", "bakery-1", 8, 16));

        Scheduler = new LlmScheduler(Llm, new LlmOptions { MaxCallsPerTick = maxLlmCallsPerTick, TimeoutSeconds = 5 }, LlmMetrics);
        var decisions = new AgentDecisionService(
            new RuleBasedPlanner(new PlannerOptions()),
            new InterestingMomentDetector(moments ?? new InterestingMomentOptions()),
            Scheduler, new PromptBuilder(), new DecisionParser(), LlmMetrics);

        Conversations = new ConversationService(Scheduler, new ConversationPromptBuilder(), new ConversationReplyParser(),
            Publisher, conversations ?? new ConversationOptions(), NullLogger<ConversationService>.Instance);

        Loop = new AgentLoop(Registry, Town, Needs, decisions, Conversations, Scheduler, Publisher, Telemetry,
            new AgentLoopOptions { PendingTimeoutTicks = pendingTimeoutTicks }, NullLogger<AgentLoop>.Instance);
    }

    public static TickOccurred Tick(long tick)
    {
        var minutes = tick * TickOccurred.MinutesPerTick;
        return new TickOccurred(tick, (int)(minutes / (24 * 60)) + 1, (int)(minutes / 60 % 24), (int)(minutes % 60));
    }

    /// <summary>Tick at a given hour of day 1.</summary>
    public static TickOccurred At(int hour, int minute = 0) => Tick((hour * 60 + minute) / TickOccurred.MinutesPerTick);

    public void Dispose()
    {
        Scheduler.Dispose();
        LlmMetrics.Dispose();
        Telemetry.Dispose();
    }
}

internal sealed class RecordingPublisher : IAgentEventPublisher
{
    public List<ActionRequested> Actions { get; } = [];
    public List<AgentThought> Thoughts { get; } = [];
    public List<ConversationMessage> Conversations { get; } = [];
    public Func<ActionRequested, Exception?> FailWith { get; set; } = _ => null;

    public Task PublishActionAsync(ActionRequested action, long tick, CancellationToken cancellationToken = default)
    {
        if (FailWith(action) is { } error)
            throw error;
        lock (Actions) Actions.Add(action);
        return Task.CompletedTask;
    }

    public Task PublishThoughtAsync(AgentThought thought, long tick, CancellationToken cancellationToken = default)
    {
        lock (Thoughts) Thoughts.Add(thought);
        return Task.CompletedTask;
    }

    public Task PublishConversationAsync(ConversationMessage message, long tick, CancellationToken cancellationToken = default)
    {
        lock (Conversations) Conversations.Add(message);
        return Task.CompletedTask;
    }
}
