using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using ZVec.Rag.Schema;

namespace ZVec.Rag.LLamaSharp;

/// <summary>
/// <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> adapter over LLamaSharp embeddings.
/// </summary>
[RequiresUnreferencedCode("LLamaSharp native GGUF loading is not trim-safe for Native AOT.")]
public sealed class LLamaSharpEmbedder : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly ILlamaSharpSession _session;
    private readonly bool _ownsSession;
    private bool _disposed;

    /// <summary>Initializes a new instance with the given options.</summary>
    public LLamaSharpEmbedder(LLamaSharpOptions options)
        : this(new LlamaSharpNativeSession(options), ownsSession: true)
    {
    }

    /// <summary>Initializes a new instance with an existing session (for tests).</summary>
    internal LLamaSharpEmbedder(ILlamaSharpSession session, bool ownsSession = false)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _ownsSession = ownsSession;
    }

    /// <inheritdoc />
    public EmbeddingGeneratorMetadata Metadata { get; } = new(LLamaSharpConstants.EmbedderModelId);

    /// <inheritdoc />
    public TService? GetService<TService>(object? key = null) where TService : class => null;

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc />
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var embeddings = new List<Embedding<float>>();
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(LLamaSharpErrorMessages.EmptyEmbedInput(), nameof(values));
            }

            ReadOnlyMemory<float> vector = await _session.EmbedAsync(value, cancellationToken).ConfigureAwait(false);
            if (vector.Length != ZVecRagRecordV1.DefaultDimensions)
            {
                throw new InvalidOperationException(
                    LLamaSharpErrorMessages.EmbeddingDimensionMismatch(
                        vector.Length,
                        ZVecRagRecordV1.DefaultDimensions));
            }

            embeddings.Add(new Embedding<float>(vector));
        }

        return new GeneratedEmbeddings<Embedding<float>>(embeddings);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsSession)
        {
            _session.Dispose();
        }
    }
}
