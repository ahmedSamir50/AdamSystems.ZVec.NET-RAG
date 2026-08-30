using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Ingestion;
using ZVec.Rag.Models;
using ZVec.Rag.Testing;
using ZVec.Rag.Testing.Evaluation;

namespace ZVec.Rag.Tests.Evaluation;

public sealed class SectionSummaryRecallLiftTests
{
    [Fact]
    public async Task RecallAtKLift_TreatmentIncludesGoldChild_WhenQueryTokenOnlyInSummary()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        const string queryToken = "Ypsilon";
        const string sourceUri = "fixture://recall-lift-doc";
        string corpus = await File.ReadAllTextAsync(GetFixturePath("section-summary-corpus.txt"), TestContext.Current.CancellationToken);

        var chat = new FakeChatClient(_ =>
            $"Technical note: the {queryToken} battery module is referenced here.");

        await using var baselineProvider = RagTestHarness.CreateServiceProvider(
            storagePath + "-baseline",
            chatClient: chat,
            generateSummaries: false,
            embedder: RagTestHarness.CreateSemanticEmbedder(),
            retrieveTopK: 5);

        await using var treatmentProvider = RagTestHarness.CreateServiceProvider(
            storagePath + "-treatment",
            chatClient: chat,
            generateSummaries: true,
            embedder: RagTestHarness.CreateSemanticEmbedder(),
            retrieveTopK: 5);

        string goldChunkId = ZVecChunkIdGenerator.Compute(
            sourceUri,
            ZVecChunkIdGenerator.DefaultStrategyId,
            chunkIndex: 0);

        var baselineIds = await RetrieveRankedChunkIdsAsync(
            baselineProvider,
            corpus,
            queryToken,
            generateSummaries: false,
            sourceUri);

        var treatmentIds = await RetrieveRankedChunkIdsAsync(
            treatmentProvider,
            corpus,
            queryToken,
            generateSummaries: true,
            sourceUri);

        var evaluator = new DeterministicEvaluator();
        RagRetrievalMetrics baselineMetrics = evaluator.EvaluateRetrieval([goldChunkId], baselineIds, k: 5);
        RagRetrievalMetrics treatmentMetrics = evaluator.EvaluateRetrieval([goldChunkId], treatmentIds, k: 5);
        RecallAtKLiftResult lift = evaluator.RecallAtKLift(baselineMetrics, treatmentMetrics, k: 5);

        Assert.Contains(goldChunkId, treatmentIds.Take(5));
        Assert.True(lift.Treatment >= lift.Baseline);
    }

    private static async Task<IReadOnlyList<string>> RetrieveRankedChunkIdsAsync(
        ServiceProvider provider,
        string corpus,
        string query,
        bool generateSummaries,
        string sourceUri)
    {
        using var scope = provider.CreateScope();
        IRagIngestor ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();
        IRagRetriever retriever = scope.ServiceProvider.GetRequiredService<IRagRetriever>();

        await ingestor.IngestTextAsync(
            corpus,
            "recall-lift-doc",
            new IngestOptions
            {
                GenerateSummaries = generateSummaries,
                SourceUri = sourceUri,
                SummarySectionMaxTokens = 4096
            },
            TestContext.Current.CancellationToken);

        var citations = await retriever.RetrieveAsync(query, topK: 5, TestContext.Current.CancellationToken);
        return citations.Select(c => c.ChunkId).ToList();
    }

    private static string GetFixturePath(string relativePath)
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", relativePath);
}
