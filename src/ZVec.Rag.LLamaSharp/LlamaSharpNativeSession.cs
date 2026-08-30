using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using LLama;
using LLama.Common;
using LLama.Sampling;
using Microsoft.Extensions.AI;

namespace ZVec.Rag.LLamaSharp;

/// <summary>
/// Production <see cref="ILlamaSharpSession"/> backed by LLamaSharp GGUF weights.
/// </summary>
internal sealed class LlamaSharpNativeSession : ILlamaSharpSession
{
    private readonly LLamaWeights _weights;
    private readonly ModelParams _parameters;
    private bool _disposed;

    /// <summary>Initializes a new instance from <paramref name="options"/>.</summary>
    public LlamaSharpNativeSession(LLamaSharpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ModelPath))
        {
            throw new ArgumentException(LLamaSharpErrorMessages.ModelPathRequired(), nameof(options));
        }

        _parameters = new ModelParams(options.ModelPath)
        {
            ContextSize = (uint)options.ContextSize,
            GpuLayerCount = options.GpuLayerCount
        };
        _weights = LLamaWeights.LoadFromFile(_parameters);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> GenerateStreamingAsync(
        IReadOnlyList<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var executor = new StatelessExecutor(_weights, _parameters);
        var inferenceParams = new InferenceParams
        {
            MaxTokens = 256,
            AntiPrompts = new List<string> { "User:" }
        };

        string prompt = BuildPrompt(messages);
        await foreach (string token in executor.InferAsync(prompt, inferenceParams, cancellationToken)
            .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(token))
            {
                yield return token;
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(LLamaSharpErrorMessages.EmptyEmbedInput(), nameof(text));
        }

        var embedder = new LLamaEmbedder(_weights, _parameters);
        IReadOnlyList<float[]> embeddings = await embedder.GetEmbeddings(text).ConfigureAwait(false);
        if (embeddings.Count == 0)
        {
            throw new InvalidOperationException(LLamaSharpErrorMessages.NoEmbeddingsReturned());
        }

        return embeddings[0];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _weights.Dispose();
    }

    private static string BuildPrompt(IReadOnlyList<ChatMessage> messages)
    {
        var builder = new System.Text.StringBuilder();
        foreach (ChatMessage message in messages)
        {
            string role = message.Role == ChatRole.System ? "System"
                : message.Role == ChatRole.User ? "User"
                : "Assistant";
            builder.Append(role).Append(": ").AppendLine(message.Text);
        }

        builder.Append("Assistant: ");
        return builder.ToString();
    }
}
