using System.Text.Json;
using AgentTown.Llm.Client;

namespace AgentTown.Llm.Conversations;

/// <summary>A validated conversation line from the LLM.</summary>
public sealed record ConversationReply(string Say, bool End, string? Agreement, int Tone);

/// <summary>Result of <see cref="ConversationReplyParser.Parse"/>. Either a reply or an error.</summary>
public sealed record ConversationReplyResult(ConversationReply? Reply, string? Error)
{
    public bool Success => Reply is not null;
}

/// <summary>Parses and cleans the JSON answer for one conversation line.</summary>
public sealed class ConversationReplyParser
{
    public const int MaxSayLength = 300;
    public const int MaxAgreementLength = 200;
    public const int MinTone = -5;
    public const int MaxTone = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public ConversationReplyResult Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new(null, "Empty response.");

        RawReply? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<RawReply>(JsonText.Extract(raw), JsonOptions);
        }
        catch (JsonException ex)
        {
            return new(null, $"Invalid JSON: {ex.Message}");
        }

        var say = Clean(parsed?.Say, MaxSayLength);
        if (say is null)
            return new(null, "Missing 'say'.");

        var agreement = Clean(parsed!.Agreement, MaxAgreementLength);
        if (agreement is not null && agreement.Equals("null", StringComparison.OrdinalIgnoreCase))
            agreement = null;

        var tone = (int)Math.Clamp(Math.Round(parsed.Tone ?? 0, MidpointRounding.AwayFromZero), MinTone, MaxTone);
        return new(new ConversationReply(say, parsed.End ?? false, agreement, tone), null);
    }

    private static string? Clean(string? value, int maxLength)
    {
        var trimmed = value?.Trim().Trim('"').Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private sealed record RawReply(string? Say, bool? End, string? Agreement, double? Tone);
}
