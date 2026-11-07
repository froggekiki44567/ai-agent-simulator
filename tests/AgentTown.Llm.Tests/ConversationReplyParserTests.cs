using AgentTown.Llm.Conversations;
using FluentAssertions;

namespace AgentTown.Llm.Tests;

public class ConversationReplyParserTests
{
    private readonly ConversationReplyParser _parser = new();

    [Fact]
    public void Parses_valid_reply()
    {
        var result = _parser.Parse("""{"say":"Shall we meet at the cafe tomorrow?","end":true,"agreement":"meet at the cafe tomorrow","tone":3}""");

        result.Success.Should().BeTrue(result.Error);
        result.Reply.Should().Be(new ConversationReply("Shall we meet at the cafe tomorrow?", true, "meet at the cafe tomorrow", 3));
    }

    [Fact]
    public void Missing_fields_get_defaults()
    {
        var result = _parser.Parse("""{"say":"Hi there."}""");

        result.Reply.Should().Be(new ConversationReply("Hi there.", false, null, 0));
    }

    [Fact]
    public void Accepts_code_fences_and_extra_text()
    {
        var result = _parser.Parse("Sure!\n```json\n{\"say\":\"Good morning!\",\"tone\":1}\n```");

        result.Reply!.Say.Should().Be("Good morning!");
    }

    [Theory]
    [InlineData(null, "Empty")]
    [InlineData("  ", "Empty")]
    [InlineData("Hello, how are you?", "Invalid JSON")]
    [InlineData("{\"say\":", "Invalid JSON")]
    [InlineData("{\"end\":true}", "Missing 'say'")]
    [InlineData("{\"say\":\"   \"}", "Missing 'say'")]
    [InlineData("{\"say\":\"Hi\",\"tone\":\"warm\"}", "Invalid JSON")]
    public void Rejects_malformed_replies(string? raw, string expectedError)
    {
        var result = _parser.Parse(raw);

        result.Success.Should().BeFalse();
        result.Error.Should().StartWith(expectedError);
    }

    [Theory]
    [InlineData("12", 5)]
    [InlineData("-9", -5)]
    [InlineData("2.5", 3)]
    [InlineData("-0.4", 0)]
    public void Tone_is_rounded_and_clamped(string tone, int expected)
    {
        var result = _parser.Parse($$"""{"say":"Hm.","tone":{{tone}}}""");

        result.Reply!.Tone.Should().Be(expected);
    }

    [Fact]
    public void Literal_null_agreement_is_treated_as_none()
    {
        var result = _parser.Parse("""{"say":"Bye.","agreement":"null"}""");

        result.Reply!.Agreement.Should().BeNull();
    }

    [Fact]
    public void Long_text_is_truncated()
    {
        var result = _parser.Parse($$"""{"say":"{{new string('a', 500)}}"}""");

        result.Reply!.Say.Should().HaveLength(ConversationReplyParser.MaxSayLength);
    }
}
