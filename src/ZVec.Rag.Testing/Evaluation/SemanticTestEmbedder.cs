using Microsoft.Extensions.AI;

namespace ZVec.Rag.Testing.Evaluation;

/// <summary>
/// Token-overlap embedder that preserves lexical rank order for retrieval metric tests.
/// </summary>
public sealed class SemanticTestEmbedder : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly int _dimensions;

    /// <summary>Initializes with the default 768-dimensional vectors.</summary>
    public SemanticTestEmbedder()
        : this(768)
    {
    }

    /// <summary>Initializes with a custom vector dimension.</summary>
    public SemanticTestEmbedder(int dimensions)
    {
        if (dimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions), dimensions, "Dimensions must be positive.");
        }

        _dimensions = dimensions;
    }

    /// <inheritdoc />
    public EmbeddingGeneratorMetadata Metadata { get; } = new("semantic-test-embedder");

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

    /// <summary>Creates a unit-length bag-of-token vector for <paramref name="text"/>.</summary>
    public ReadOnlyMemory<float> CreateVector(string text)
    {
        var vector = new float[_dimensions];
        foreach (string token in Tokenize(text))
        {
            int bucket = Math.Abs(StringComparer.Ordinal.GetHashCode(token)) % _dimensions;
            vector[bucket] += 1f;
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

    private static IEnumerable<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (string token in text.Split(
                     [' ', '\t', '\r', '\n', '.', ',', ';', ':', '!', '?'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return token.ToLowerInvariant();
        }
    }
}
