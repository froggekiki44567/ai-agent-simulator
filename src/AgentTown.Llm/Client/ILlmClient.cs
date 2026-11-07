using System.Text.Json;

namespace AgentTown.Llm.Client;

/// <summary>A prompt for the LLM: fixed instructions (system) plus the situation (user).</summary>
public sealed record LlmPrompt(string System, string User);

/// <summary>
/// Talks to the language model. Production uses <see cref="OllamaLlmClient"/>;
/// tests always use <see cref="FakeLlmClient"/> and never call a real Ollama.
/// </summary>
public interface ILlmClient
{
    /// <summary>Asks the model for a JSON answer and returns the raw text.</summary>
    Task<string> CompleteAsync(LlmPrompt prompt, CancellationToken cancellationToken = default);

    /// <summary>Turns text into an embedding vector (for memory similarity search).</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>True if the model server answers and both models are installed.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

public static class LlmClientExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Asks for JSON and deserializes it into <typeparamref name="T"/>.</summary>
    /// <exception cref="JsonException">The model did not return valid JSON for <typeparamref name="T"/>.</exception>
    public static async Task<T> CompleteJsonAsync<T>(this ILlmClient client, LlmPrompt prompt, CancellationToken cancellationToken = default)
    {
        var text = await client.CompleteAsync(prompt, cancellationToken);
        return JsonSerializer.Deserialize<T>(JsonText.Extract(text), JsonOptions)
            ?? throw new JsonException("The model returned null.");
    }
}
