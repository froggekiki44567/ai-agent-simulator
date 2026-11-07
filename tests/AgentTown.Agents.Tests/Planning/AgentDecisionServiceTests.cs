using AgentTown.Agents.Planning;
using AgentTown.Contracts.Model;
using AgentTown.Llm;
using AgentTown.Llm.Client;
using AgentTown.Llm.Decisions;
using AgentTown.Llm.Prompts;
using AgentTown.Llm.Scheduling;
using FluentAssertions;
using Xunit;

namespace AgentTown.Agents.Tests.Planning;

public sealed class AgentDecisionServiceTests : IDisposable
{
    private static readonly Guid OnaId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly JobInfo BakerJob = new("job-baker", "bakery-1", 8, 16);

    private readonly FakeLlmClient _llm = new();
    private readonly LlmMetrics _metrics = new();
    private readonly LlmScheduler _scheduler;
    private readonly AgentDecisionService _service;

    public AgentDecisionServiceTests()
    {
        _scheduler = new LlmScheduler(_llm, new LlmOptions { MaxCallsPerTick = 1, TimeoutSeconds = 5 }, _metrics);
        _service = new AgentDecisionService(
            new RuleBasedPlanner(new PlannerOptions()),
            new InterestingMomentDetector(new InterestingMomentOptions()),
            _scheduler,
            new PromptBuilder(),
            new DecisionParser(),
            _metrics);
    }

    private static DecisionInput Input(long tick = 100, int hour = 18, bool withOna = true, string? townEvent = null) => new(
        tick, Day: 1, hour, Minute: 0, BakerJob,
        NearbyAgents: withOna ? [new(OnaId, "Ona")] : [],
        KnownBuildings: [new("bakery-1", "Bakery", true), new("home-1", "Home", true), new("park-1", "Park", true)],
        townEvent);

    [Fact]
    public async Task Routine_moment_uses_rules_and_never_calls_llm()
    {
        var agent = TestAgents.Create(location: "home-1", social: 90);

        var decision = await _service.DecideAsync(agent, Input(hour: 10));

        decision.Source.Should().Be(DecisionSource.Rules);
        decision.Request.Type.Should().Be(ActionType.Move, "it's shift time, go to the bakery");
        _llm.Prompts.Should().BeEmpty();
    }

    [Fact]
    public async Task Interesting_moment_gets_llm_decision_and_thought()
    {
        // Smoke test (Day 9): one agent gets one LLM decision.
        _llm.Enqueue($$"""{"action":"Talk","target":"{{OnaId}}","say":"Ona! Want to grab a coffee?","thought":"I feel lonely, Ona is right here.","importance":4}""");
        var agent = TestAgents.Create(social: 20);

        var decision = await _service.DecideAsync(agent, Input(tick: 100));

        decision.Source.Should().Be(DecisionSource.Llm);
        decision.Moment!.Kind.Should().Be(MomentKind.SocialChance);
        decision.Request.Should().BeEquivalentTo(new { Type = ActionType.Talk, TargetId = OnaId.ToString(), Say = "Ona! Want to grab a coffee?" });
        decision.Importance.Should().Be(4);
        decision.ToThought().Text.Should().Be("I feel lonely, Ona is right here.");
        agent.LastLlmDecisionTick.Should().Be(100);

        _llm.Prompts.Single().User.Should().Contain("You are Jonas").And.Contain("Ona (id " + OnaId);
    }

    [Fact]
    public async Task Bad_llm_answer_falls_back_to_rules()
    {
        _llm.Enqueue("""{"action":"Teleport","target":"moon"}""");
        var agent = TestAgents.Create(social: 20);

        var decision = await _service.DecideAsync(agent, Input());

        decision.Source.Should().Be(DecisionSource.Rules);
        decision.FallbackReason.Should().StartWith("InvalidResponse").And.Contain("Teleport");
        agent.LastLlmDecisionTick.Should().BeNull("a failed LLM call doesn't start the cooldown");
    }

    [Fact]
    public async Task Hallucinated_target_falls_back_to_rules()
    {
        _llm.Enqueue("""{"action":"Move","target":"casino-1"}""");

        var decision = await _service.DecideAsync(TestAgents.Create(social: 20), Input());

        decision.Source.Should().Be(DecisionSource.Rules);
        decision.FallbackReason.Should().Contain("casino-1");
    }

    [Fact]
    public async Task Used_up_budget_falls_back_without_calling_llm()
    {
        _scheduler.OnTick(100);
        await _service.DecideAsync(TestAgents.Create(social: 20), Input());   // uses the only call

        var decision = await _service.DecideAsync(TestAgents.Create(social: 20), Input());

        decision.Source.Should().Be(DecisionSource.Rules);
        decision.FallbackReason.Should().Be(nameof(LlmFailure.BudgetExceeded));
        _llm.Prompts.Should().HaveCount(1);
    }

    [Fact]
    public async Task Town_event_reaches_the_prompt()
    {
        var agent = TestAgents.Create();

        await _service.DecideAsync(agent, Input(hour: 12, townEvent: "Bread prices doubled."));

        _llm.Prompts.Single().User.Should().Contain("What just happened in town:").And.Contain("Bread prices doubled.");
    }

    public void Dispose()
    {
        _scheduler.Dispose();
        _metrics.Dispose();
    }
}
