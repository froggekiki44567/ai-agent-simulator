using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace AgentTown.Llm.Client;

/// <summary>
/// In-memory <see cref="ILlmClient"/> for tests and offline runs. Never calls a real model.
/// Answers are queued with <see cref="Enqueue"/>, or produced by <see cref="Responder"/>.
/// Embeddings are deterministic: the same text always gives the same vector.
/// </summary>
public sealed class FakeLlmClient : ILlmClient
{
    public const int EmbeddingDimensions = 768;

    private readonly ConcurrentQueue<string> _responses = new();
    private readonly ConcurrentQueue<LlmPrompt> _prompts = new();

    /// <summary>Used when the queue is empty. Default: an Idle decision.</summary>
    public Func<LlmPrompt, string> Responder { get; set; } =
        _ => """{"action":"Idle","target":null,"item":null,"quantity":0,"amount":0,"say":null,"thought":"Nothing special.","importance":1}""";

    /// <summary>Simulated response time.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public bool Available { get; set; } = true;

    /// <summary>Every prompt received, in order.</summary>
    public IReadOnlyCollection<LlmPrompt> Prompts => _prompts;

    public FakeLlmClient Enqueue(params string[] responses)
    {
        foreach (var response in responses)
            _responses.Enqueue(response);
        return this;
    }

    public async Task<string> CompleteAsync(LlmPrompt prompt, CancellationToken cancellationToken = default)
    {
        _prompts.Enqueue(prompt);

        if (Delay > TimeSpan.Zero)
            await Task.Delay(Delay, cancellationToken);

        return _responses.TryDequeue(out var response) ? response : Responder(prompt);
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        // Seed a random generator from a hash of the text, then normalize to length 1.
        var seed = BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        var random = new Random(seed);
        var vector = new float[EmbeddingDimensions];
        for (var i = 0; i < vector.Length; i++)
            vector[i] = (float)(random.NextDouble() * 2 - 1);

        var length = MathF.Sqrt(vector.Sum(v => v * v));
        for (var i = 0; i < vector.Length; i++)
            vector[i] /= length;

        return Task.FromResult(vector);
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(Available);
}
