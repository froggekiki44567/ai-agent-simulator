using AgentTown.Agents.Mind;
using AgentTown.Agents.Relationships;
using AgentTown.Agents.Tests.Loop;
using AgentTown.Contracts.Events;
using AgentTown.Contracts.Model;
using FluentAssertions;
using Xunit;

namespace AgentTown.Agents.Tests.Relationships;

public sealed class RelationshipLoopTests : IDisposable
{
    private readonly LoopHarness _h = new(maxLlmCallsPerTick: 10);

    private (Agents.State.AgentState Jonas, Agents.State.AgentState Ona) TwoInThePark(double jonasSocial = 20)
    {
        var ona = TestAgents.Create(location: "park-1", jobId: null, name: "Ona");
        var jonas = TestAgents.Create(location: "park-1", social: jonasSocial, jobId: null, name: "Jonas");
        _h.Registry.Add(ona);
        _h.Registry.Add(jonas);
        return (jonas, ona);
    }

    /// <summary>Jonas talks to Ona; in the conversation Jonas speaks with tone 4 and Ona with tone -2.</summary>
    private void JonasTalksToOna(Agents.State.AgentState ona) =>
        _h.Llm.Responder = p => p.System.Contains("ONE line of a conversation")
            ? p.User.StartsWith("Speaker: Jonas") ? """{"say":"Lovely day!","tone":4}""" : """{"say":"Hmm, I guess.","tone":-2}"""
            : p.User.Contains($"(id {ona.Id})") && p.User.StartsWith("You are Jonas")
                ? $$"""{"action":"Talk","target":"{{ona.Id}}","say":"Hi Ona!","thought":"I want company.","importance":4}"""
                : """{"action":"Idle","thought":"Relaxing.","importance":1}""";

    private async Task RunTalkAsync(Agents.State.AgentState jonas)
    {
        await _h.Loop.OnTickAsync(LoopHarness.At(12));
        await _h.Loop.WaitForDecisionsAsync();
        var talk = _h.Publisher.Actions.Single(a => a.AgentId == jonas.Id && a.Type == ActionType.Talk);
        _h.Loop.OnActionResult(new ActionResult(jonas.Id, talk.RequestId, true, null, AgentStateDelta.None));
        await _h.Loop.WaitForDecisionsAsync();
    }

    [Fact]
    public async Task Conversation_tone_changes_both_relationships()
    {
        var (jonas, ona) = TwoInThePark();
        JonasTalksToOna(ona);

        await RunTalkAsync(jonas);

        (await _h.RelationshipStore.GetAsync(jonas.Id, ona.Id))!.Affinity.Should().Be(8);
        (await _h.RelationshipStore.GetAsync(ona.Id, jonas.Id))!.Affinity.Should().Be(-4);
    }

    [Fact]
    public async Task Conversation_prompt_says_how_the_speaker_sees_the_listener()
    {
        var (jonas, ona) = TwoInThePark();
        await _h.RelationshipStore.UpsertAsync(new RelationshipRecord(jonas.Id, ona.Id, 40, 0));
        JonasTalksToOna(ona);

        await RunTalkAsync(jonas);

        var lines = _h.Llm.Prompts.Where(p => p.System.Contains("ONE line of a conversation")).ToArray();
        lines.Should().Contain(p => p.User.StartsWith("Speaker: Jonas") && p.User.Contains("To Jonas, Ona is a good friend."));
        lines.Should().Contain(p => p.User.StartsWith("Speaker: Ona") && p.User.Contains("Ona doesn't know Jonas well."));
    }

    [Fact]
    public async Task Decision_prompt_contains_relationships_and_known_people()
    {
        var (jonas, ona) = TwoInThePark();
        var petras = TestAgents.Create(location: "bakery-1", name: "Petras");
        _h.Registry.Add(petras);
        await _h.RelationshipStore.UpsertAsync(new RelationshipRecord(jonas.Id, ona.Id, 40, 0));
        await _h.RelationshipStore.UpsertAsync(new RelationshipRecord(jonas.Id, petras.Id, 0, 72, Balance: 20m));
        JonasTalksToOna(ona);

        await _h.Loop.OnTickAsync(LoopHarness.At(12));
        await _h.Loop.WaitForDecisionsAsync();

        var prompt = _h.Llm.Prompts.Single(p => p.User.StartsWith("You are Jonas"));
        prompt.User.Should()
            .Contain($"- Ona (id {ona.Id}), a good friend")
            .And.Contain("People you know (not here):\n- Petras: an acquaintance who owes you 20.00 EUR (last met day 1, 12:00)")
            .And.Contain("Right now: Your friend Ona is here.");
    }

    [Fact]
    public async Task Debtor_nearby_triggers_an_llm_decision()
    {
        var (jonas, ona) = TwoInThePark(jonasSocial: 90);
        await _h.RelationshipStore.UpsertAsync(new RelationshipRecord(jonas.Id, ona.Id, 0, 0, Balance: 15m));

        await _h.Loop.OnTickAsync(LoopHarness.At(12));
        await _h.Loop.WaitForDecisionsAsync();

        _h.Llm.Prompts.Should().ContainSingle(p => p.User.StartsWith("You are Jonas"))
            .Which.User.Should().Contain("Right now: Ona is here and still owes you 15.00 EUR.");
    }

    [Fact]
    public async Task Transfer_between_agents_updates_the_relationship()
    {
        var (jonas, ona) = TwoInThePark();

        _h.Loop.OnTransaction(new TransactionCompleted(Guid.NewGuid(), jonas.Id, ona.Id, 30m, TransactionKinds.Transfer, "for flour", 40));
        await _h.Loop.WaitForDecisionsAsync();

        (await _h.RelationshipStore.GetAsync(ona.Id, jonas.Id)).Should().BeEquivalentTo(new { Affinity = 3, Balance = -30m });
        (await _h.RelationshipStore.GetAsync(jonas.Id, ona.Id))!.Balance.Should().Be(30m);
    }

    [Fact]
    public async Task Wage_from_a_non_agent_account_creates_no_relationship()
    {
        var (jonas, _) = TwoInThePark();

        _h.Loop.OnTransaction(new TransactionCompleted(Guid.NewGuid(), Guid.NewGuid(), jonas.Id, 80m, TransactionKinds.Wage, null, 96));
        await _h.Loop.WaitForDecisionsAsync();

        (await _h.RelationshipStore.GetByAgentAsync(jonas.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Mind_query_shows_relationships_and_newest_memories()
    {
        var (jonas, ona) = TwoInThePark();
        JonasTalksToOna(ona);
        await RunTalkAsync(jonas);
        await _h.MemoryWriter.RememberAsync(jonas.Id, Agents.Memory.MemoryKind.Observation, "Something later.", 2, tick: 500);
        var mind = new AgentMindQuery(_h.Memories, _h.Relationships);

        var relationships = await mind.GetRelationshipsAsync(jonas.Id);
        var memories = await mind.GetMemoriesAsync(jonas.Id, limit: 3);

        relationships.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            OtherAgentId = ona.Id,
            OtherName = "Ona",
            Affinity = 8,
            Label = "an acquaintance",
            Balance = 0m,
        });
        memories.Should().HaveCount(3);
        memories[0].Text.Should().Be("Something later.");
        memories.Should().Contain(m => m.Kind == "Conversation");
    }

    [Fact]
    public async Task Mind_query_for_an_unknown_agent_is_empty()
    {
        var mind = new AgentMindQuery(_h.Memories, _h.Relationships);

        (await mind.GetMemoriesAsync(Guid.NewGuid())).Should().BeEmpty();
        (await mind.GetRelationshipsAsync(Guid.NewGuid())).Should().BeEmpty();
    }

    public void Dispose() => _h.Dispose();
}
