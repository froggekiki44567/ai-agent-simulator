using AgentTown.Llm.Conversations;
using FluentAssertions;

namespace AgentTown.Llm.Tests;

public class ConversationPromptBuilderTests
{
    private static readonly ConversationParticipant Jonas = new("Jonas", 45, ["proud", "kind"], "Own the best bakery", "Short and warm");
    private static readonly ConversationParticipant Ona = new("Ona", 30, ["curious", "talkative"], "Open a cafe", "Lots of questions", "an old friend");

    private readonly ConversationPromptBuilder _builder = new();

    [Fact]
    public void Contains_both_personalities_and_the_place()
    {
        var prompt = _builder.Build(new ConversationPromptContext { Speaker = Jonas, Listener = Ona, PlaceId = "park-1", Day = 2, Hour = 9, Minute = 30 });

        prompt.System.Should().Contain("\"say\"").And.Contain("\"tone\"");
        prompt.User.Should().Contain("Speaker: Jonas, 45").And.Contain("proud, kind").And.Contain("Short and warm")
            .And.Contain("Listener: Ona, 30").And.Contain("curious, talkative")
            .And.Contain("Ona is an old friend")
            .And.Contain("day 2, 09:30, at park-1")
            .And.Contain("Jonas starts the conversation");
    }

    [Fact]
    public void Includes_history_in_order_and_last_line_hint()
    {
        var prompt = _builder.Build(new ConversationPromptContext
        {
            Speaker = Ona,
            Listener = Jonas,
            History = [new("Jonas", "Morning, Ona!"), new("Ona", "Hi Jonas, fresh bread today?"), new("Jonas", "Always.")],
            IsLastLine = true,
        });

        prompt.User.Should().Contain("Jonas: Morning, Ona!\nOna: Hi Jonas, fresh bread today?\nJonas: Always.")
            .And.Contain("This is the last line")
            .And.Contain("doesn't know Jonas well")
            .And.NotContain("starts the conversation");
    }
}
