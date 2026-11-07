using AgentTown.Agents.State;
using AgentTown.Agents.Tests.Loop;
using AgentTown.Contracts.Events;
using AgentTown.Contracts.Model;
using FluentAssertions;
using Xunit;

namespace AgentTown.Agents.Tests.Memory;

public sealed class MemoryLoopTests : IDisposable
{
    private readonly LoopHarness _h = new(maxLlmCallsPerTick: 10);

    [Fact]
    public async Task Action_result_becomes_a_memory()
    {
        var jonas = TestAgents.Create(location: "bakery-1", money: 50);
        _h.Registry.Add(jonas);
        jonas.PendingAction = new PendingAction(Guid.NewGuid(), ActionType.Buy, "bakery-1", 0, "bread", 1);

        _h.Loop.OnActionResult(new ActionResult(jonas.Id, jonas.PendingAction.RequestId, true, null, new AgentStateDelta(Money: 47m)));
        await _h.Loop.WaitForDecisionsAsync();

        (await _h.Memories.GetByAgentAsync(jonas.Id)).Single().Text.Should().Be("I bought 1 bread at bakery-1 for 3.00 EUR.");
    }

    [Fact]
    public async Task Going_bankrupt_is_remembered_as_very_important()
    {
        var jonas = TestAgents.Create(money: 1);
        _h.Registry.Add(jonas);
        jonas.PendingAction = new PendingAction(Guid.NewGuid(), ActionType.Move, "park-1", 0);

        _h.Loop.OnActionResult(new ActionResult(jonas.Id, jonas.PendingAction.RequestId, true, null, new AgentStateDelta(Money: -5m, Status: AgentStatus.Bankrupt)));
        await _h.Loop.WaitForDecisionsAsync();

        (await _h.Memories.GetByAgentAsync(jonas.Id)).Should().ContainSingle(m => m.Importance == 9);
    }

    [Fact]
    public async Task Wage_transaction_is_remembered_by_the_worker()
    {
        var jonas = TestAgents.Create();
        _h.Registry.Add(jonas);

        _h.Loop.OnTransaction(new TransactionCompleted(Guid.NewGuid(), Guid.NewGuid(), jonas.Id, 80m, TransactionKinds.Wage, null, 96));
        await _h.Loop.WaitForDecisionsAsync();

        (await _h.Memories.GetByAgentAsync(jonas.Id)).Single().Text.Should().Be("I got paid 80.00 EUR for my work.");
    }

    [Fact]
    public async Task Llm_prompt_contains_relevant_memories()
    {
        var jonas = TestAgents.Create(location: "home-1", money: 3);  // low money → LLM moment
        _h.Registry.Add(jonas);
        await _h.MemoryWriter.RememberAsync(jonas.Id, Agents.Memory.MemoryKind.Observation, "Petras owes me 20 EUR.", 6, tick: 0);

        await _h.Loop.OnTickAsync(LoopHarness.At(10));
        await _h.Loop.WaitForDecisionsAsync();

        var prompt = _h.Llm.Prompts.Should().ContainSingle().Subject;
        prompt.User.Should().Contain("What you remember:").And.Contain("day 1, 00:00: Petras owes me 20 EUR.");
    }

    [Fact]
    public async Task Llm_decision_and_conversation_are_remembered()
    {
        var ona = TestAgents.Create(location: "park-1", jobId: null, name: "Ona");
        var jonas = TestAgents.Create(location: "park-1", social: 20, jobId: null, name: "Jonas");
        _h.Registry.Add(ona);
        _h.Registry.Add(jonas);
        _h.Llm.Responder = p => p.System.Contains("ONE line of a conversation")
            ? """{"say":"Nice to see you!","tone":2}"""
            : p.User.Contains($"(id {ona.Id})")
                ? $$"""{"action":"Talk","target":"{{ona.Id}}","say":"Hi Ona!","thought":"I want company.","importance":4}"""
                : """{"action":"Idle","thought":"Relaxing.","importance":1}""";

        await _h.Loop.OnTickAsync(LoopHarness.At(12));
        await _h.Loop.WaitForDecisionsAsync();
        var talk = _h.Publisher.Actions.Single(a => a.AgentId == jonas.Id);
        _h.Loop.OnActionResult(new ActionResult(jonas.Id, talk.RequestId, true, null, AgentStateDelta.None));
        await _h.Loop.WaitForDecisionsAsync();

        var jonasMemories = await _h.Memories.GetByAgentAsync(jonas.Id);
        jonasMemories.Should().Contain(m => m.Text == "I decided to talk: I want company." && m.Importance == 4);
        jonasMemories.Should().Contain(m => m.Kind == Agents.Memory.MemoryKind.Conversation && m.Text.StartsWith("I talked with Ona. I: \"Hi Ona!\""));
        (await _h.Memories.GetByAgentAsync(ona.Id)).Should().Contain(m => m.Kind == Agents.Memory.MemoryKind.Conversation);
    }

    public void Dispose() => _h.Dispose();
}
