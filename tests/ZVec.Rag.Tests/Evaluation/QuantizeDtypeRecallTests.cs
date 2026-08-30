using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ZVec.Extensions.VectorData.Store;
using ZVec.NET;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Ingestion;
using ZVec.Rag.Models;
using ZVec.Rag.Schema;
using ZVec.Rag.Testing;
using ZVec.Rag.Testing.Evaluation;

namespace ZVec.Rag.Tests.Evaluation;

/// <summary>
/// Measures Recall@K across quantization dtypes on the Story 2.8 seed fixture for benchmarks.md.
/// </summary>
public sealed class QuantizeDtypeRecallTests
{
    [Fact]
    public async Task QuantizeDtypes_ProduceRecallAtK_OnFixtureQuery()
    {
        var embedder = new SemanticTestEmbedder(ZVecRagRecordV1.DefaultDimensions);
        string qaLine = File.ReadAllLines(GetFixturePath("qa.jsonl"))[0];
        using JsonDocument qaDoc = JsonDocument.Parse(qaLine);
        string query = qaDoc.RootElement.GetProperty("query").GetString()!;
        var goldIds = qaDoc.RootElement.GetProperty("gold_chunk_ids")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToList();
        int k = 5;

        double fp32Recall = await MeasureRecallAsync(embedder, ZVecQuantizeType.Undefined, query, goldIds, k);
        double fp16Recall = await MeasureRecallAsync(embedder, ZVecQuantizeType.Fp16, query, goldIds, k);
        double int8Recall = await MeasureRecallAsync(embedder, ZVecQuantizeType.Int8, query, goldIds, k);

        Console.WriteLine($"Fixture Recall@{k}: FP32={fp32Recall:F3}, FP16={fp16Recall:F3}, INT8={int8Recall:F3}");

        // Fixture numbers are mirrored in docs/reference/benchmarks.md (Story 4.3.2).
        Assert.Equal(1.000d, fp32Recall, precision: 3);
        Assert.Equal(1.000d, fp16Recall, precision: 3);

        var evaluator = new DeterministicEvaluator();
        RecallAtKLiftResult fp16Lift = evaluator.RecallAtKLift(
            new RagRetrievalMetrics(fp32Recall, 0d, 0d, k),
            new RagRetrievalMetrics(fp16Recall, 0d, 0d, k),
            k);

        if (fp16Lift.Baseline > 0d)
        {
            double ratio = fp16Lift.Treatment / fp16Lift.Baseline;
            Assert.True(ratio >= 0.95d, $"Fp16 recall ratio {ratio} below 0.95 gate.");
        }
        else
        {
            Assert.True(fp16Lift.Delta >= 0d);
        }

        RecallAtKLiftResult int8Lift = evaluator.RecallAtKLift(
            new RagRetrievalMetrics(fp32Recall, 0d, 0d, k),
            new RagRetrievalMetrics(int8Recall, 0d, 0d, k),
            k);

        if (int8Lift.Baseline > 0d && int8Lift.Treatment / int8Lift.Baseline < 0.95d)
        {
            Console.WriteLine(
                $"Int8 recall ratio {int8Lift.Treatment / int8Lift.Baseline:F3} below 0.95 (informational only).");
        }
    }

    private static async Task<double> MeasureRecallAsync(
        SemanticTestEmbedder embedder,
        ZVecQuantizeType quantizeType,
        string query,
        IReadOnlyList<string> goldIds,
        int k)
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);

        try
        {
            RagRetrievalMetrics metrics = await EvaluateStorageAsync(
                storagePath,
                embedder,
                quantizeType,
                query,
                goldIds,
                k);
            return metrics.RecallAtK;
        }
        finally
        {
            try
            {
                Directory.Delete(storagePath, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static async Task<RagRetrievalMetrics> EvaluateStorageAsync(
        string storagePath,
        SemanticTestEmbedder embedder,
        ZVecQuantizeType quantizeType,
        string query,
        IReadOnlyList<string> goldIds,
        int k)
    {
        var services = new ServiceCollection();
        services.AddZVecRag(opts =>
        {
            opts.StoragePath = storagePath;
            opts.Embedder = embedder;
            opts.Chat = new FakeChatClient("ok");
            opts.VectorStore.ModelId = "quantize-dtype-bench";
            opts.RetrieveTopK = k;
            opts.VectorStore.DefaultQuantizeType = quantizeType;
        })
        .AddTokenChunker(maxTokens: 64, overlapTokens: 8);

        await using ServiceProvider provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();
        var retriever = scope.ServiceProvider.GetRequiredService<IRagRetriever>();

        string orionText = await File.ReadAllTextAsync(GetFixturePath("corpus/doc-orion.md"));
        string zephyrText = await File.ReadAllTextAsync(GetFixturePath("corpus/doc-zephyr.md"));
        await ingestor.IngestTextAsync(orionText, "doc-orion", new IngestOptions { SourceUri = "fixture://doc-orion.md" });
        await ingestor.IngestTextAsync(zephyrText, "doc-zephyr", new IngestOptions { SourceUri = "fixture://doc-zephyr.md" });

        var citations = await retriever.RetrieveAsync(query, topK: k);
        var evaluator = new DeterministicEvaluator();
        return evaluator.EvaluateRetrieval(goldIds, citations.Select(c => c.ChunkId).ToList(), k);
    }

    private static string GetFixturePath(string relativePath)
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", relativePath);
}
