using AgentTown.Agents.Conversations;
using AgentTown.Agents.Tests.Loop;
using AgentTown.Contracts.Events;
using AgentTown.Contracts.Model;
using FluentAssertions;
using Xunit;

namespace AgentTown.Agents.Tests.Conversations;

public sealed class ConversationLoopTests : IDisposable
{
    private readonly LoopHarness _h = new(maxLlmCallsPerTick: 10);

    [Fact]
    public async Task Accepted_talk_starts_a_conversation_and_both_feel_less_lonely()
    {
        var ona = TestAgents.Create(location: "park-1", social: 60, jobId: null, name: "Ona");
        var jonas = TestAgents.Create(location: "park-1", social: 20, jobId: null, name: "Jonas");
        _h.Registry.Add(ona);
        _h.Registry.Add(jonas);
        _h.Llm.Responder = p => p.System.Contains("ONE line of a conversation")
            ? """{"say":"Nice to see you!","end":false,"agreement":null,"tone":2}"""
            : p.User.Contains($"(id {ona.Id})")
                ? $$"""{"action":"Talk","target":"{{ona.Id}}","say":"Hi Ona!","thought":"I want company.","importance":4}"""
                : """{"action":"Idle","thought":"Just relaxing."}""";

        ConversationOutcome? finished = null;
        _h.Loop.ConversationFinished += o => finished = o;

        await _h.Loop.OnTickAsync(LoopHarness.At(12));
        await _h.Loop.WaitForDecisionsAsync();
        var talk = _h.Publisher.Actions.Single(a => a.AgentId == jonas.Id && a.Type == ActionType.Talk);
        var onaSocialBefore = ona.Social;
        var jonasSocialBefore = jonas.Social;

        _h.Loop.OnActionResult(new ActionResult(jonas.Id, talk.RequestId, true, null, AgentStateDelta.None));
        await _h.Loop.WaitForDecisionsAsync();

        finished.Should().NotBeNull();
        _h.Publisher.Conversations.Should().HaveCount(4);
        _h.Publisher.Conversations[0].Should().BeEquivalentTo(new { Text = "Hi Ona!", SpeakerId = jonas.Id, ListenerId = ona.Id });
        _h.Publisher.Conversations[^1].IsFinal.Should().BeTrue();
        jonas.Social.Should().BeGreaterThan(jonasSocialBefore);
        ona.Social.Should().BeGreaterThan(onaSocialBefore);
        _h.Loop.IsTalking(jonas.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Rejected_talk_starts_no_conversation()
    {
        var ona = TestAgents.Create(location: "park-1", jobId: null, name: "Ona");
        var jonas = TestAgents.Create(location: "park-1", social: 20, jobId: null, name: "Jonas");
        _h.Registry.Add(ona);
        _h.Registry.Add(jonas);
        _h.Llm.Responder = p => p.User.Contains($"(id {ona.Id})")
            ? $$"""{"action":"Talk","target":"{{ona.Id}}","say":"Hi!","thought":"Company.","importance":4}"""
            : """{"action":"Idle","thought":"Relaxing."}""";

        await _h.Loop.OnTickAsync(LoopHarness.At(12));
        await _h.Loop.WaitForDecisionsAsync();
        var talk = _h.Publisher.Actions.Single(a => a.AgentId == jonas.Id);

        _h.Loop.OnActionResult(new ActionResult(jonas.Id, talk.RequestId, false, "Target is not here", AgentStateDelta.None));
        await _h.Loop.WaitForDecisionsAsync();

        _h.Publisher.Conversations.Should().BeEmpty();
    }

    [Fact]
    public async Task Agents_in_a_conversation_are_not_asked_to_decide()
    {
        var ona = TestAgents.Create(location: "park-1", jobId: null, name: "Ona");
        var jonas = TestAgents.Create(location: "park-1", social: 20, jobId: null, name: "Jonas");
        _h.Registry.Add(ona);
        _h.Registry.Add(jonas);
        _h.Llm.Responder = p => p.System.Contains("ONE line of a conversation")
            ? """{"say":"Mm-hm.","tone":0}"""
            : p.User.Contains($"(id {ona.Id})")
                ? $$"""{"action":"Talk","target":"{{ona.Id}}","say":"Hi!","thought":"Company.","importance":4}"""
                : """{"action":"Idle","thought":"Relaxing."}""";

        await _h.Loop.OnTickAsync(LoopHarness.At(12));
        await _h.Loop.WaitForDecisionsAsync();
        var talk = _h.Publisher.Actions.Single(a => a.AgentId == jonas.Id);

        _h.Llm.Delay = TimeSpan.FromMilliseconds(200);
        _h.Loop.OnActionResult(new ActionResult(jonas.Id, talk.RequestId, true, null, AgentStateDelta.None));
        _h.Loop.IsTalking(ona.Id).Should().BeTrue();

        var actionsBefore = _h.Publisher.Actions.Count;
        await _h.Loop.OnTickAsync(LoopHarness.At(12, 10));
        _h.Publisher.Actions.Count.Should().Be(actionsBefore, "both agents are busy talking");

        await _h.Loop.WaitForDecisionsAsync();
        _h.Loop.IsTalking(ona.Id).Should().BeFalse();
    }

    public void Dispose() => _h.Dispose();
}
