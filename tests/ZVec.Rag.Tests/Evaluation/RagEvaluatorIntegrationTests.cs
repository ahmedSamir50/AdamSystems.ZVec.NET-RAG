using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Ingestion;
using ZVec.Rag.Models;
using ZVec.Rag.Schema;
using ZVec.Rag.Testing;
using ZVec.Rag.Testing.Evaluation;
namespace ZVec.Rag.Tests.Evaluation;

public sealed class RagEvaluatorIntegrationTests
{
    [Fact]
    public async Task EvaluateFixtureQuery_RetrievesGoldChunkId_WithSemanticTestEmbedder()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);

        try
        {
            var embedder = new CountingEmbedder(ZVecRagRecordV1.DefaultDimensions);
            var services = new ServiceCollection();
            services.AddZVecRag(opts =>
            {
                opts.StoragePath = storagePath;
                opts.Embedder = embedder;
                opts.Chat = new FakeChatClient("ok");
                opts.VectorStore.ModelId = "eval-fixture-v1";
                opts.RetrieveTopK = 5;
            })
            .AddTokenChunker(maxTokens: 64, overlapTokens: 8);

            await using ServiceProvider provider = services.BuildServiceProvider();
            await using AsyncServiceScope scope = provider.CreateAsyncScope();
            IRagIngestor ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();
            IRagRetriever retriever = scope.ServiceProvider.GetRequiredService<IRagRetriever>();

            string orionText = await File.ReadAllTextAsync(
                GetFixturePath("corpus/doc-orion.md"),
                TestContext.Current.CancellationToken);
            string zephyrText = await File.ReadAllTextAsync(
                GetFixturePath("corpus/doc-zephyr.md"),
                TestContext.Current.CancellationToken);

            await ingestor.IngestTextAsync(
                orionText,
                "doc-orion",
                new IngestOptions { SourceUri = "fixture://doc-orion.md" },
                cancellationToken: TestContext.Current.CancellationToken);
            await ingestor.IngestTextAsync(
                zephyrText,
                "doc-zephyr",
                new IngestOptions { SourceUri = "fixture://doc-zephyr.md" },
                cancellationToken: TestContext.Current.CancellationToken);
            int generateCallsAfterIngest = embedder.GenerateAsyncCalls;
            string goldOrion = ZVecChunkIdGenerator.Compute(
                "fixture://doc-orion.md",
                ZVecChunkIdGenerator.DefaultStrategyId,
                chunkIndex: 0);

            var citations = await retriever.RetrieveAsync(
                "Orion battery 5V output",
                topK: 5,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(generateCallsAfterIngest + 1, embedder.GenerateAsyncCalls);
            Assert.Contains(citations, c => c.ChunkId == goldOrion);
            Assert.All(citations, c => Assert.InRange(c.DenseScore, 0f, 1f));

            var evaluator = new DeterministicEvaluator();
            RagRetrievalMetrics metrics = evaluator.EvaluateRetrieval(
                [goldOrion],
                citations.Select(c => c.ChunkId).ToList(),
                k: 5);

            Assert.InRange(metrics.RecallAtK, 0d, 1d);
            Assert.True(metrics.RecallAtK > 0d);
            Assert.True(metrics.Mrr > 0d);
        }
        finally
        {
            if (Directory.Exists(storagePath))
            {
                try { Directory.Delete(storagePath, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public void FixtureQaJsonl_HasExpectedSchema()
    {
        string qaPath = GetFixturePath("qa.jsonl");
        string[] lines = File.ReadAllLines(qaPath);
        Assert.True(lines.Length >= 2);

        using JsonDocument doc = JsonDocument.Parse(lines[0]);
        Assert.True(doc.RootElement.TryGetProperty("id", out _));
        Assert.True(doc.RootElement.TryGetProperty("query", out _));
        Assert.True(doc.RootElement.TryGetProperty("gold_chunk_ids", out _));
    }

    private static string GetFixturePath(string relativePath)
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", relativePath);

    /// <summary>Counts <see cref="IEmbeddingGenerator{TInput,TEmbedding}.GenerateAsync"/> calls so retrieve cannot hide a re-embed path.</summary>
    private sealed class CountingEmbedder : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly SemanticTestEmbedder _inner;

        public CountingEmbedder(int dimensions)
        {
            _inner = new SemanticTestEmbedder(dimensions);
        }

        public int GenerateAsyncCalls { get; private set; }

        public EmbeddingGeneratorMetadata Metadata => _inner.Metadata;

        public TService? GetService<TService>(object? key = null) where TService : class
            => _inner.GetService<TService>(key);

        public object? GetService(Type serviceType, object? serviceKey = null)
            => _inner.GetService(serviceType, serviceKey);

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            GenerateAsyncCalls++;
            return _inner.GenerateAsync(values, options, cancellationToken);
        }

        public void Dispose() => _inner.Dispose();
    }
}

