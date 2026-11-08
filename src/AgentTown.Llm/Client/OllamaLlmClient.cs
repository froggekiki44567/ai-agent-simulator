using System.Text;
using OllamaSharp;
using OllamaSharp.Models;

namespace AgentTown.Llm.Client;

/// <summary><see cref="ILlmClient"/> backed by a local Ollama server via OllamaSharp.</summary>
public sealed class OllamaLlmClient(IOllamaApiClient ollama, LlmOptions options) : ILlmClient
{
    public async Task<string> CompleteAsync(LlmPrompt prompt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var request = new GenerateRequest
        {
            Model = options.Model,
            System = prompt.System,
            Prompt = prompt.User,
            Format = "json",
            Stream = false,
            Options = new RequestOptions { Temperature = options.Temperature },
        };

        var text = new StringBuilder();
        await foreach (var chunk in ollama.GenerateAsync(request, cancellationToken))
        {
            text.Append(chunk?.Response);
        }

        return text.ToString();
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var response = await ollama.EmbedAsync(new EmbedRequest { Model = options.EmbeddingModel, Input = [text] }, cancellationToken);
        return response.Embeddings is [var first, ..]
            ? first
            : throw new InvalidOperationException("Ollama returned no embedding.");
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await ollama.ListLocalModelsAsync(cancellationToken);
            var names = models.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return HasModel(names, options.Model) && HasModel(names, options.EmbeddingModel);
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    // Ollama lists "nomic-embed-text:latest" for a model pulled as "nomic-embed-text".
    private static bool HasModel(HashSet<string> installed, string model) =>
        installed.Contains(model) || installed.Contains(model + ":latest");
}
