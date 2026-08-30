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

namespace ZVec.Rag.Tests.Samples;

public sealed class Sample03RecallGateTests
{
    [Fact]
    public async Task Fp16RecallAtK_IsAtLeast95PercentOfFp32Baseline_OnFixtureQuery()
    {
        string baselinePath = RagTestHarness.CreateTempStoragePath();
        string treatmentPath = RagTestHarness.CreateTempStoragePath();
        Directory.CreateDirectory(baselinePath);
        Directory.CreateDirectory(treatmentPath);

        try
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

            RagRetrievalMetrics baselineMetrics = await EvaluateStorageAsync(
                baselinePath,
                embedder,
                ZVecQuantizeType.Undefined,
                query,
                goldIds,
                k);
            RagRetrievalMetrics treatmentMetrics = await EvaluateStorageAsync(
                treatmentPath,
                embedder,
                ZVecQuantizeType.Fp16,
                query,
                goldIds,
                k);

            var evaluator = new DeterministicEvaluator();
            RecallAtKLiftResult lift = evaluator.RecallAtKLift(baselineMetrics, treatmentMetrics, k);

            if (lift.Baseline > 0d)
            {
                double ratio = lift.Treatment / lift.Baseline;
                Assert.True(ratio >= 0.95d, $"Fp16 recall ratio {ratio} below 0.95 gate.");
            }
            else
            {
                Assert.True(lift.Delta >= 0d);
            }

            string int8Path = RagTestHarness.CreateTempStoragePath();
            Directory.CreateDirectory(int8Path);
            try
            {
                RagRetrievalMetrics int8Metrics = await EvaluateStorageAsync(
                    int8Path,
                    embedder,
                    ZVecQuantizeType.Int8,
                    query,
                    goldIds,
                    k);
                RecallAtKLiftResult int8Lift = evaluator.RecallAtKLift(baselineMetrics, int8Metrics, k);
                if (int8Lift.Baseline > 0d && int8Lift.Treatment / int8Lift.Baseline < 0.95d)
                {
                    Console.WriteLine(
                        $"Int8 recall ratio {int8Lift.Treatment / int8Lift.Baseline:F3} below 0.95 (informational only).");
                }
            }
            finally
            {
                try { Directory.Delete(int8Path, recursive: true); } catch { }
            }
        }
        finally
        {
            try { Directory.Delete(baselinePath, recursive: true); } catch { }
            try { Directory.Delete(treatmentPath, recursive: true); } catch { }
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
            opts.VectorStore.ModelId = "sample03-gate";
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
