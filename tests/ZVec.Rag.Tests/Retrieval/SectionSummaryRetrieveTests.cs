using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Constants;
using ZVec.Rag.Ingestion;
using ZVec.Rag.Models;
using ZVec.Rag.Retrieval;
using ZVec.Rag.Schema;

namespace ZVec.Rag.Tests.Retrieval;

public sealed class SectionSummaryRetrieveTests
{
    [Fact]
    public async Task RetrieveAsync_WhenGenerateSummariesFalse_DoesNotRequireSummaryCollection()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        var chat = new ZVec.Rag.Testing.FakeChatClient(_ => "unused summary");
        await using var provider = RagTestHarness.CreateServiceProvider(
            storagePath,
            chatClient: chat,
            generateSummaries: false,
            embedder: RagTestHarness.CreateSemanticEmbedder());

        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<ZVec.Rag.Abstractions.IRagIngestor>();
        var retriever = scope.ServiceProvider.GetRequiredService<ZVec.Rag.Abstractions.IRagRetriever>();

        await ingestor.IngestTextAsync(
            "retrieval without summary flag",
            "doc-retrieve-off",
            new IngestOptions { GenerateSummaries = false },
            TestContext.Current.CancellationToken);

        var citations = await retriever.RetrieveAsync("retrieval", topK: 3, TestContext.Current.CancellationToken);
        Assert.NotEmpty(citations);
    }

    [Fact]
    public async Task RetrieveAsync_WhenGenerateSummariesTrue_RetrievesGoldChildViaSummaryPath()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        const string queryToken = "Ypsilon";
        const string sourceUri = "fixture://summary-retrieve";

        var chat = new ZVec.Rag.Testing.FakeChatClient(_ =>
            $"Overview mentioning {queryToken} module specifications.");

        await using var provider = RagTestHarness.CreateServiceProvider(
            storagePath,
            chatClient: chat,
            generateSummaries: true,
            embedder: RagTestHarness.CreateSemanticEmbedder(),
            retrieveTopK: 5);

        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<ZVec.Rag.Abstractions.IRagIngestor>();
        var retriever = scope.ServiceProvider.GetRequiredService<ZVec.Rag.Abstractions.IRagRetriever>();

        string sectionBody =
            "The power supply uses standard connectors and delivers regulated output. " +
            "The device operates within normal temperature ranges without special cooling.";

        await ingestor.IngestTextAsync(
            sectionBody,
            "summary-retrieve-doc",
            new IngestOptions
            {
                GenerateSummaries = true,
                SourceUri = sourceUri,
                SummarySectionMaxTokens = 4096
            },
            TestContext.Current.CancellationToken);

        string goldChunkId = ZVecChunkIdGenerator.Compute(
            sourceUri,
            ZVecChunkIdGenerator.DefaultStrategyId,
            chunkIndex: 0);

        var citations = await retriever.RetrieveAsync(queryToken, topK: 5, TestContext.Current.CancellationToken);
        Assert.Contains(citations, c => c.ChunkId == goldChunkId);
        Assert.All(citations.Where(c => c.ChunkId == goldChunkId), c => Assert.DoesNotContain(queryToken, c.Text));
    }

    [Fact]
    public void Fuse_ParentBoostedHit_RanksAboveUnboostedDistractor()
    {
        const string parentId = "summary-parent";
        var summary = new ZVecRagSectionSummaryV1
        {
            SectionSummaryId = parentId,
            Summary = "Parent summary text"
        };

        var boostedChild = new Citation(
            "doc-a", "uri-a", "hash-a", null, 0, 0, "child-boosted", "boosted text",
            0.5f, 0.5f, 0f, parentId);

        var distractor = new Citation(
            "doc-b", "uri-b", "hash-b", null, 0, 1, "child-distractor", "distractor text",
            0.9f, 0.9f, 0f);

        var queryVector = new ReadOnlyMemory<float>(new float[ZVecRagRecordV1.DefaultDimensions]);
        List<Citation> fused = SectionSummaryFusion.Fuse(
            [boostedChild, distractor],
            [(summary, Score: 0.4f)],
            expandedChildren: [],
            queryVector,
            parentBoost: 1.0f);

        Citation boosted = fused.Single(c => c.ChunkId == "child-boosted");
        Citation plain = fused.Single(c => c.ChunkId == "child-distractor");
        Assert.True(boosted.RankScore > plain.RankScore);
        Assert.Equal("Parent summary text", boosted.SectionSummary);
    }
}
