using System.Text;
using AgentTown.Llm.Client;

namespace AgentTown.Llm.Conversations;

/// <summary>One person in a conversation, as the LLM sees them.</summary>
/// <param name="Relationship">How the speaker sees this person (e.g. "a good friend"), if known.</param>
public sealed record ConversationParticipant(
    string Name,
    int Age,
    IReadOnlyList<string> Traits,
    string Goal,
    string SpeakingStyle,
    string? Relationship = null);

/// <summary>A line that was already said.</summary>
public sealed record ConversationLine(string SpeakerName, string Text);

/// <summary>Everything needed to generate the next line of a conversation.</summary>
public sealed record ConversationPromptContext
{
    public required ConversationParticipant Speaker { get; init; }
    public required ConversationParticipant Listener { get; init; }
    public IReadOnlyList<ConversationLine> History { get; init; } = [];
    public string? PlaceId { get; init; }
    public int Day { get; init; }
    public int Hour { get; init; }
    public int Minute { get; init; }

    /// <summary>True when this is the last line allowed, so the speaker should wrap up.</summary>
    public bool IsLastLine { get; init; }

    public IReadOnlyList<string> Memories { get; init; } = [];
}

/// <summary>
/// Builds the prompt for one line of a conversation between two agents. Each line is generated
/// separately, from the speaker's point of view, using both personalities.
/// </summary>
public sealed class ConversationPromptBuilder
{
    public static string SystemPrompt { get; } = """
        You write ONE line of a conversation between two people living in a small town simulation called AgentTown.
        You speak as the speaker only. Stay in character: follow their personality, goal and speaking style.
        Keep it short and natural: one or two sentences, no narration, no stage directions.
        If both people agree on something concrete (meet somewhere, help each other, lend money), put it in "agreement".
        "tone" is how the speaker feels about the listener after this line, from -5 (hostile) to 5 (warm).

        Answer with ONE JSON object and nothing else:
        {"say":"<what the speaker says>","end":<true if the speaker ends the conversation>,"agreement":"<short summary or null>","tone":<-5..5>}
        """;

    public LlmPrompt Build(ConversationPromptContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var speaker = context.Speaker;
        var listener = context.Listener;
        var text = new StringBuilder();

        text.AppendLine($"Speaker: {speaker.Name}, {speaker.Age} years old.");
        text.AppendLine($"Personality: {string.Join(", ", speaker.Traits)}.");
        text.AppendLine($"Goal: {speaker.Goal}");
        text.AppendLine($"Speaking style: {speaker.SpeakingStyle}");
        text.AppendLine();
        text.AppendLine($"Listener: {listener.Name}, {listener.Age} years old.");
        text.AppendLine($"Personality: {string.Join(", ", listener.Traits)}.");
        text.AppendLine(listener.Relationship is null
            ? $"{speaker.Name} doesn't know {listener.Name} well."
            : $"To {speaker.Name}, {listener.Name} is {listener.Relationship}.");
        text.AppendLine();
        text.AppendLine($"Now: day {context.Day}, {context.Hour:00}:{context.Minute:00}, at {context.PlaceId ?? "the street"}.");

        if (context.Memories.Count > 0)
        {
            text.AppendLine();
            text.AppendLine($"What {speaker.Name} remembers:");
            foreach (var memory in context.Memories)
                text.AppendLine($"- {memory}");
        }

        text.AppendLine();
        if (context.History.Count == 0)
        {
            text.AppendLine($"{speaker.Name} starts the conversation.");
        }
        else
        {
            text.AppendLine("Conversation so far:");
            foreach (var line in context.History)
                text.AppendLine($"{line.SpeakerName}: {line.Text}");
        }

        if (context.IsLastLine)
            text.AppendLine($"This is the last line: {speaker.Name} wraps up the conversation (end = true).");

        text.AppendLine();
        text.Append($"What does {speaker.Name} say next? Answer with the JSON object only.");
        return new LlmPrompt(SystemPrompt, text.ToString());
    }
}
