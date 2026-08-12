using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData;
using ZVec.NET;
using ZVec.NET.Mapping;

namespace ZVecRagSample;

/// <summary>
/// Console sample demonstrating a minimal local-first RAG flow over the ZVec.NET
/// embedded vector engine: ingest document chunks, run a vectorized search, and
/// print ranked results. Embeddings are mocked with deterministic vectors so the
/// sample runs without an external embedding service.
/// </summary>
public static class Program
{
    public static async Task Main()
    {
        System.Console.WriteLine("=== ZVec.NET-RAG Local RAG Sample ===");
        System.Console.WriteLine();

        // 1. Initialize the local embedded vector store.
        var storagePath = Path.Combine(Path.GetTempPath(), "ZVecRagSample", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storagePath);

        using var factory = new ZVecFactory();
        var options = new ZVecVectorStoreOptions { StoragePath = storagePath };
        var store = new ZVecVectorStore(factory, options);
        var collection = store.GetCollection<string, RagDocumentChunk>("documents");
        await collection.EnsureCollectionExistsAsync(CancellationToken.None);
        System.Console.WriteLine($"Store initialized at: {storagePath}");
        System.Console.WriteLine();

        // 2. Ingest sample document chunks (mocked embeddings — deterministic per chunk).
        var chunks = new[]
        {
            new RagDocumentChunk
            {
                Id = "chunk_1",
                Content = "ZVec.NET is an embedded vector database engine for .NET applications.",
                Source = "architecture.md",
                Embedding = MockEmbedding("zvec embedded vector database")
            },
            new RagDocumentChunk
            {
                Id = "chunk_2",
                Content = "RAG combines retrieval of relevant context with generation to ground answers.",
                Source = "rag-pipeline.md",
                Embedding = MockEmbedding("rag retrieval generation grounded")
            },
            new RagDocumentChunk
            {
                Id = "chunk_3",
                Content = "Microsoft.Extensions.VectorData provides a unified abstraction over vector stores.",
                Source = "vectordata-connector.md",
                Embedding = MockEmbedding("vector data abstraction microsoft extensions")
            },
            new RagDocumentChunk
            {
                Id = "chunk_4",
                Content = "Native AOT compilation trims unused code to produce small self-contained binaries.",
                Source = "native-aot-memory.md",
                Embedding = MockEmbedding("native aot trim binary compilation")
            }
        };

        await collection.UpsertAsync(chunks, CancellationToken.None);
        System.Console.WriteLine($"Indexed {chunks.Length} document chunks.");
        System.Console.WriteLine();

        // 3. Run a vectorized search (mocked query embedding).
        var query = "How does RAG combine retrieval and generation?";
        var queryVector = MockEmbedding("rag retrieval generation grounded");
        System.Console.WriteLine($"Query: {query}");
        System.Console.WriteLine("Top results:");

        var topResults = new List<VectorSearchResult<RagDocumentChunk>>();
        await foreach (var result in collection.SearchAsync(queryVector, top: 2, cancellationToken: CancellationToken.None))
        {
            topResults.Add(result);
        }

        for (int i = 0; i < topResults.Count; i++)
        {
            var r = topResults[i];
            System.Console.WriteLine($"  {i + 1}. [score={r.Score:F4}] {r.Record.Content}");
            System.Console.WriteLine($"     source: {r.Record.Source}");
        }

        // 4. Demonstrate filtered search (hybrid: vector + metadata filter).
        System.Console.WriteLine();
        System.Console.WriteLine("Filtered search (source = rag-pipeline.md):");
        System.Linq.Expressions.Expression<Func<RagDocumentChunk, bool>> filter = x => x.Source == "rag-pipeline.md";
        var filteredResults = new List<VectorSearchResult<RagDocumentChunk>>();
        var filteredOptions = new VectorSearchOptions<RagDocumentChunk> { Filter = filter };
        await foreach (var result in collection.SearchAsync(
            queryVector,
            top: 2,
            options: filteredOptions,
            cancellationToken: CancellationToken.None))
        {
            filteredResults.Add(result);
        }

        foreach (var r in filteredResults)
        {
            System.Console.WriteLine($"  [score={r.Score:F4}] {r.Record.Content}");
        }

        // 5. Cleanup.
        await collection.EnsureCollectionDeletedAsync(CancellationToken.None);
        try { Directory.Delete(storagePath, recursive: true); } catch { }

        System.Console.WriteLine();
        System.Console.WriteLine("=== Sample complete ===");
    }

    /// <summary>
    /// Produces a deterministic 4-dimensional mock embedding for a given text by
    /// hashing the text into four float buckets. Real RAG pipelines replace this
    /// with a call to <c>IEmbeddingGenerator</c> from Microsoft.Extensions.AI.
    /// </summary>
    /// <param name="text">Text to embed.</param>
    /// <returns>A 4-dimensional float vector pinned as <see cref="ReadOnlyMemory{T}"/>.</returns>
    private static ReadOnlyMemory<float> MockEmbedding(string text)
    {
        var vector = new float[4];
        for (int i = 0; i < text.Length; i++)
        {
            vector[i % 4] += text[i];
        }

        // Normalize to unit length so cosine similarity is meaningful.
        float norm = MathF.Sqrt(vector[0] * vector[0] + vector[1] * vector[1] +
                                 vector[2] * vector[2] + vector[3] * vector[3]);
        if (norm > 0f)
        {
            for (int i = 0; i < 4; i++)
            {
                vector[i] /= norm;
            }
        }

        return vector;
    }
}

/// <summary>
/// Sample document chunk record for the RAG console sample. Decorated with both
/// ZVec native mapping attributes and Microsoft.Extensions.VectorData attributes.
/// </summary>
public sealed class RagDocumentChunk
{
    /// <summary>Unique chunk identifier.</summary>
    [ZVecId]
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>Chunk text content used for full-text search and display.</summary>
    [ZVecField]
    [VectorStoreData(IsIndexed = true, IsFullTextIndexed = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Source document URI or path for citation tracking.</summary>
    [ZVecField]
    [VectorStoreData(IsIndexed = true)]
    public string Source { get; set; } = string.Empty;

    /// <summary>Dense embedding vector for semantic search.</summary>
    [ZVecVector(4)]
    [VectorStoreVector(4)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
