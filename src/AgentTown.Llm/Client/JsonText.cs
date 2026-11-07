namespace AgentTown.Llm.Client;

/// <summary>Helpers for cleaning up model output before parsing it as JSON.</summary>
public static class JsonText
{
    /// <summary>
    /// Removes Markdown code fences (```json ... ```) and any text around the outermost JSON object.
    /// Small models often add these even when told to return only JSON.
    /// </summary>
    public static string Extract(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text.Trim();
    }
}
