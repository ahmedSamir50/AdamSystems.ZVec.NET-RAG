using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;
using ZVec.Rag.Ingestion;
using ZVec.Rag.Models;
using ZVec.Rag.Options;
using ZVec.Rag.Schema;
using ZVec.Rag.Testing;
using ZVec.Rag.Testing.Evaluation;

namespace ZVec.Rag.Tests.Ingestion;

public sealed class SummaryCollectionNameTests
{
    [Fact]
    public async Task DefaultPair_UsesRagSectionSummaries()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        await using var provider = RagTestHarness.CreateServiceProvider(
            storagePath,
            chatClient: new FakeChatClient(_ => "Concise section overview."));
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();

        await ingestor.IngestTextAsync(
            "Alpha beta gamma delta epsilon.",
            "default-summary-doc",
            new IngestOptions { GenerateSummaries = true, SummarySectionMaxTokens = 4096 },
            TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(Path.Combine(storagePath, ZVecRagConstants.SectionSummaryCollectionName)));
    }

    [Fact]
    public async Task CustomChunkCollection_ConventionSuffix_WhenSummaryUnset()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        var services = new ServiceCollection();
        services.AddZVecRag(opts =>
        {
            opts.StoragePath = storagePath;
            opts.Embedder = new DeterministicEmbedder(ZVecRagRecordV1.DefaultDimensions);
            opts.Chat = new FakeChatClient(_ => "Legal section overview.");
            opts.CollectionName = "legal_chunks";
            opts.VectorStore.ModelId = "legal-model";
        }).AddTokenChunker();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();

        await ingestor.IngestTextAsync(
            "Contract clause alpha beta gamma.",
            "legal-doc",
            new IngestOptions { GenerateSummaries = true, SummarySectionMaxTokens = 4096 },
            TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(Path.Combine(storagePath, "legal_chunks_summaries")));
        Assert.False(Directory.Exists(Path.Combine(storagePath, ZVecRagConstants.SectionSummaryCollectionName)));
    }

    [Fact]
    public async Task ExplicitSummaryName_WinsOverConvention()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        var services = new ServiceCollection();
        services.AddZVecRag(opts =>
        {
            opts.StoragePath = storagePath;
            opts.Embedder = new DeterministicEmbedder(ZVecRagRecordV1.DefaultDimensions);
            opts.Chat = new FakeChatClient(_ => "Explicit summary overview.");
            opts.CollectionName = "legal_chunks";
            opts.SummaryCollectionName = "my_summaries";
            opts.VectorStore.ModelId = "explicit-summary-model";
        }).AddTokenChunker();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();

        await ingestor.IngestTextAsync(
            "Explicit naming clause alpha beta.",
            "explicit-doc",
            new IngestOptions { GenerateSummaries = true, SummarySectionMaxTokens = 4096 },
            TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(Path.Combine(storagePath, "my_summaries")));
        Assert.False(Directory.Exists(Path.Combine(storagePath, "legal_chunks_summaries")));
    }

    [Fact]
    public void WhitespaceSummaryName_Throws()
    {
        var opts = new ZVecRagOptions { SummaryCollectionName = "  " };

        var ex = Assert.Throws<ArgumentException>(() => opts.ResolveSummaryCollectionName());

        Assert.StartsWith(ZVecRagErrorMessages.NullOrEmptySummaryCollectionName(), ex.Message);
        Assert.Equal(nameof(ZVecRagOptions.SummaryCollectionName), ex.ParamName);
    }
}
