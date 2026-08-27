using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;

namespace ZVec.Rag.Testing;

/// <summary>
/// Hash-based deterministic embedder for fast, network-free RAG pipeline tests.
/// </summary>
public sealed class DeterministicEmbedder : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly int _dimensions;

    /// <summary>Initializes a new instance with the default 768-dimensional vectors.</summary>
    public DeterministicEmbedder()
        : this(768)
    {
    }

    /// <summary>Initializes a new instance with a custom vector dimension.</summary>
    public DeterministicEmbedder(int dimensions)
    {
        if (dimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions), dimensions, "Dimensions must be positive.");
        }

        _dimensions = dimensions;
    }

    /// <inheritdoc />
    public EmbeddingGeneratorMetadata Metadata { get; } = new("deterministic-embedder");

    /// <inheritdoc />
    public TService? GetService<TService>(object? key = null) where TService : class => null;

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc />
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var embeddings = new List<Embedding<float>>();
        foreach (string value in values)
        {
            embeddings.Add(new Embedding<float>(CreateVector(value)));
        }

        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>Creates a unit-length vector from the hash of <paramref name="text"/>.</summary>
    public ReadOnlyMemory<float> CreateVector(string text)
    {
        var vector = new float[_dimensions];
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty));

        for (int i = 0; i < _dimensions; i++)
        {
            vector[i] = (hash[i % hash.Length] / 255f) - 0.5f;
        }

        float magnitude = 0f;
        for (int i = 0; i < vector.Length; i++)
        {
            magnitude += vector[i] * vector[i];
        }

        magnitude = MathF.Sqrt(magnitude);
        if (magnitude > 0f)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= magnitude;
            }
        }

        return vector;
    }
}
