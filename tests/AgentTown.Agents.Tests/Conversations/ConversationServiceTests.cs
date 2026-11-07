using AgentTown.Agents.Conversations;
using AgentTown.Agents.Tests.Loop;
using AgentTown.Contracts.Model;
using FluentAssertions;
using Xunit;

namespace AgentTown.Agents.Tests.Conversations;

public sealed class ConversationServiceTests : IDisposable
{
    private static readonly ConversationScene Scene = new(Tick: 70, Day: 1, Hour: 11, Minute: 40, PlaceId: "park-1");

    private readonly LoopHarness _h = new(maxLlmCallsPerTick: 10);
    private readonly Agents.State.AgentState _jonas = TestAgents.Create(location: "park-1", name: "Jonas");
    private readonly Agents.State.AgentState _ona = TestAgents.Create(location: "park-1", name: "Ona");

    private static string Line(string say, bool end = false, string? agreement = null, int tone = 0) =>
        $$"""{"say":"{{say}}","end":{{(end ? "true" : "false")}},"agreement":{{(agreement is null ? "null" : $"\"{agreement}\"")}},"tone":{{tone}}}""";

    [Fact]
    public async Task Speakers_take_turns_up_to_max_lines()
    {
        _h.Llm.Enqueue(Line("Hi Ona!"), Line("Hi Jonas!"), Line("Nice weather."), Line("It is."));

        var outcome = await _h.Conversations.RunAsync(_jonas, _ona, openingLine: null, Scene);

        outcome.Lines.Select(l => l.Text).Should().Equal("Hi Ona!", "Hi Jonas!", "Nice weather.", "It is.");
        outcome.Lines.Select(l => l.SpeakerId).Should().Equal(_jonas.Id, _ona.Id, _jonas.Id, _ona.Id);
        outcome.Lines.Select(l => l.ListenerId).Should().Equal(_ona.Id, _jonas.Id, _ona.Id, _jonas.Id);
        outcome.Lines.Select(l => l.LineIndex).Should().Equal(0, 1, 2, 3);
        outcome.Lines.Select(l => l.IsFinal).Should().Equal(false, false, false, true);
        outcome.Lines.Should().OnlyContain(l => l.ConversationId == outcome.ConversationId);
        outcome.EndReason.Should().BeNull();
        _h.Publisher.Conversations.Should().Equal(outcome.Lines);
    }

    [Fact]
    public async Task Opening_line_from_talk_action_is_line_zero_without_an_llm_call()
    {
        _h.Llm.Enqueue(Line("Hello yourself."), Line("See you."), Line("Bye."));

        var outcome = await _h.Conversations.RunAsync(_jonas, _ona, "Hello, Ona!", Scene);

        outcome.Lines[0].Should().BeEquivalentTo(new { Text = "Hello, Ona!", SpeakerId = _jonas.Id });
        outcome.Lines[1].SpeakerId.Should().Be(_ona.Id);
        _h.Llm.Prompts.Should().HaveCount(3);
        _h.Llm.Prompts.First().User.Should().Contain("Speaker: Ona").And.Contain("Jonas: Hello, Ona!");
    }

    [Fact]
    public async Task Ends_early_when_a_speaker_ends_it_after_min_lines()
    {
        _h.Llm.Enqueue(Line("Hi!"), Line("Busy, sorry.", end: true));

        var outcome = await _h.Conversations.RunAsync(_jonas, _ona, null, Scene);

        outcome.Lines.Should().HaveCount(2);
        outcome.Lines[^1].IsFinal.Should().BeTrue();
        outcome.EndReason.Should().Be("Ended");
    }

    [Fact]
    public async Task End_before_min_lines_is_ignored()
    {
        _h.Llm.Enqueue(Line("Hi!", end: true), Line("Oh, hi."), Line("Bye.", end: true));

        var outcome = await _h.Conversations.RunAsync(_jonas, _ona, null, Scene);

        outcome.Lines.Should().HaveCount(3);
    }

    [Fact]
    public async Task Keeps_agreement_and_average_tone_per_speaker()
    {
        _h.Llm.Enqueue(
            Line("Coffee tomorrow?", tone: 4),
            Line("Yes, at the cafe at nine!", agreement: "meet at the cafe tomorrow at 9", tone: 2),
            Line("Great.", tone: 2),
            Line("See you.", tone: 0));

        var outcome = await _h.Conversations.RunAsync(_jonas, _ona, null, Scene);

        outcome.Agreement.Should().Be("meet at the cafe tomorrow at 9");
        outcome.InitiatorTone.Should().Be(3);
        outcome.PartnerTone.Should().Be(1);
    }

    [Fact]
    public async Task Llm_error_ends_conversation_with_lines_so_far()
    {
        var calls = 0;
        _h.Llm.Responder = _ => ++calls == 1 ? Line("Hello!") : throw new HttpRequestException("model down");

        var outcome = await _h.Conversations.RunAsync(_jonas, _ona, null, Scene);

        outcome.Lines.Should().ContainSingle().Which.IsFinal.Should().BeTrue();
        outcome.EndReason.Should().Be("Error");
    }

    [Fact]
    public async Task Invalid_reply_ends_conversation()
    {
        _h.Llm.Enqueue(Line("Hi!"), "I don't know what to say");

        var outcome = await _h.Conversations.RunAsync(_jonas, _ona, "Hey.", Scene);

        outcome.Lines.Should().HaveCount(2);
        outcome.EndReason.Should().StartWith("InvalidResponse");
    }

    [Fact]
    public async Task No_budget_and_no_opening_line_means_no_conversation()
    {
        using var h = new LoopHarness(maxLlmCallsPerTick: 0);

        var outcome = await h.Conversations.RunAsync(_jonas, _ona, null, Scene);

        outcome.Lines.Should().BeEmpty();
        outcome.EndReason.Should().Be("BudgetExceeded");
        h.Publisher.Conversations.Should().BeEmpty();
    }

    public void Dispose() => _h.Dispose();
}
