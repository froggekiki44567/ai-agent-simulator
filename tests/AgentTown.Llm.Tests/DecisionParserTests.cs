using AgentTown.Contracts.Model;
using AgentTown.Llm.Decisions;
using FluentAssertions;

namespace AgentTown.Llm.Tests;

public class DecisionParserTests
{
    private const string Ona = "22222222-2222-2222-2222-222222222222";
    private static readonly IReadOnlySet<string> Known = new HashSet<string> { "bakery-1", "home-1", "bank-1", Ona };
    private readonly DecisionParser _parser = new();

    [Fact]
    public void Parses_valid_decision_from_plan_example()
    {
        const string json = """
            {"action":"Buy","target":"bakery-1","item":"bread","quantity":2,"amount":0,"say":null,
             "thought":"I'm hungry and the bakery is close.","importance":3}
            """;

        var result = _parser.Parse(json, Known);

        result.Success.Should().BeTrue(result.Error);
        result.Decision.Should().BeEquivalentTo(new LlmDecision(ActionType.Buy, "bakery-1", "bread", 2, 0, null, "I'm hungry and the bakery is close.", 3));
    }

    [Theory]
    [InlineData(null, "Empty")]
    [InlineData("", "Empty")]
    [InlineData("   ", "Empty")]
    [InlineData("I think I will go to the bakery.", "Invalid JSON")]
    [InlineData("{\"action\":\"Buy\",", "Invalid JSON")]
    [InlineData("[1,2,3]", "Invalid JSON")]
    [InlineData("{\"target\":\"bakery-1\"}", "Missing 'action'")]
    [InlineData("{\"action\":\"Fly\"}", "Unknown action")]
    [InlineData("{\"action\":\"42\"}", "Unknown action")]
    public void Rejects_malformed_responses(string? raw, string expectedError)
    {
        var result = _parser.Parse(raw, Known);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain(expectedError);
    }

    [Theory]
    [InlineData("{\"action\":\"Move\"}", "needs a 'target'")]
    [InlineData("{\"action\":\"Move\",\"target\":\"casino-9\"}", "Unknown target 'casino-9'")]
    [InlineData("{\"action\":\"Buy\",\"target\":\"bakery-1\"}", "needs an 'item'")]
    [InlineData("{\"action\":\"Buy\",\"target\":\"bakery-1\",\"item\":\"bread\",\"quantity\":-2}", "cannot be negative")]
    [InlineData("{\"action\":\"Deposit\",\"target\":\"bank-1\",\"amount\":0}", "greater than 0")]
    [InlineData("{\"action\":\"Transfer\",\"target\":\"bank-1\",\"amount\":-5}", "cannot be negative")]
    [InlineData("{\"action\":\"Talk\",\"target\":\"" + Ona + "\"}", "Talk needs 'say'")]
    public void Rejects_impossible_decisions(string raw, string expectedError)
    {
        var result = _parser.Parse(raw, Known);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain(expectedError);
    }

    [Fact]
    public void Accepts_code_fences_and_text_around_json()
    {
        const string raw = """
            Sure! Here is my decision:
            ```json
            {"action":"Sleep","target":"home-1","thought":"So tired."}
            ```
            """;

        var result = _parser.Parse(raw, Known);

        result.Decision!.Action.Should().Be(ActionType.Sleep);
        result.Decision.Target.Should().Be("home-1");
    }

    [Fact]
    public void Is_lenient_about_case_numbers_as_strings_and_null_words()
    {
        const string raw = """{"Action":"buy","TARGET":"bakery-1","item":"bread","quantity":"2","say":"null","importance":"7",}""";

        var result = _parser.Parse(raw, Known);

        result.Success.Should().BeTrue(result.Error);
        result.Decision!.Should().BeEquivalentTo(new { Action = ActionType.Buy, Quantity = 2, Say = (string?)null, Importance = 7 });
    }

    [Fact]
    public void Fills_defaults_and_clamps_values()
    {
        const string raw = """{"action":"Eat","item":"bread","quantity":0,"importance":42}""";

        var decision = _parser.Parse(raw, Known).Decision!;

        decision.Quantity.Should().Be(1, "eating needs at least one item");
        decision.Importance.Should().Be(10);
        decision.Thought.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Missing_importance_uses_default()
    {
        _parser.Parse("""{"action":"Idle"}""", Known).Decision!.Importance.Should().Be(DecisionParser.DefaultImportance);
    }

    [Fact]
    public void Talk_to_known_agent_is_accepted()
    {
        var raw = $$"""{"action":"Talk","target":"{{Ona}}","say":"Good morning, Ona!","thought":"I miss chatting."}""";

        var decision = _parser.Parse(raw, Known).Decision!;

        decision.Action.Should().Be(ActionType.Talk);
        decision.Say.Should().Be("Good morning, Ona!");
    }

    [Fact]
    public void Very_long_texts_are_truncated()
    {
        var raw = $$"""{"action":"Idle","thought":"{{new string('x', 1000)}}"}""";

        _parser.Parse(raw, Known).Decision!.Thought.Should().HaveLength(DecisionParser.MaxThoughtLength);
    }

    [Fact]
    public void To_request_is_marked_as_llm_decision()
    {
        var agentId = Guid.NewGuid();
        var decision = new LlmDecision(ActionType.Talk, Ona, null, 0, 0, "Hi!", "I want to chat.", 4);

        var request = DecisionParser.ToRequest(decision, agentId);

        request.Should().BeEquivalentTo(new
        {
            AgentId = agentId,
            Type = ActionType.Talk,
            TargetId = Ona,
            Say = "Hi!",
            Reason = "I want to chat.",
            Source = DecisionSource.Llm,
        });
    }
}
