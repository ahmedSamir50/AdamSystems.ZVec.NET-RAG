using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;
using ZVec.Rag.Ingestion;
using ZVec.Rag.Models;
using ZVec.Rag.Schema;
using ZVec.Rag.Testing;
using ZVec.Rag.Testing.Evaluation;

namespace ZVec.Rag.Tests.Ingestion;

public sealed class SectionSummaryIngestTests
{
    [Fact]
    public void IngestOptions_DefaultSummaryKnobs_MatchConstants()
    {
        var options = new IngestOptions();

        Assert.False(options.GenerateSummaries);
        Assert.Equal(ZVecRagConstants.DefaultMaxSummaryTokens, options.MaxSummaryTokens);
        Assert.Equal(ZVecRagConstants.DefaultSummarySectionMaxTokens, options.SummarySectionMaxTokens);
    }

    [Fact]
    public async Task IngestTextAsync_WhenGenerateSummariesFalse_DoesNotCreateSummaryCollectionDirectory()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath);
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();

        await ingestor.IngestTextAsync(
            "plain ingest without summaries",
            "doc-off",
            cancellationToken: TestContext.Current.CancellationToken);

        string summaryPath = Path.Combine(storagePath, ZVecRagConstants.SectionSummaryCollectionName);
        Assert.False(Directory.Exists(summaryPath));
    }

    [Fact]
    public async Task IngestTextAsync_WhenGenerateSummariesTrueWithoutChat_Throws()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        var services = new ServiceCollection();
        services.AddZVecRag(opts =>
        {
            opts.StoragePath = storagePath;
            opts.Embedder = new DeterministicEmbedder(ZVecRagRecordV1.DefaultDimensions);
            opts.Chat = null;
            opts.VectorStore.ModelId = "no-chat";
        }).AddTokenChunker();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ingestor.IngestTextAsync(
                "needs chat for summaries",
                "doc-no-chat",
                new IngestOptions { GenerateSummaries = true },
                TestContext.Current.CancellationToken));

        Assert.Equal(ZVecRagErrorMessages.ChatClientNotConfigured(), ex.Message);
    }

    [Fact]
    public async Task IngestTextAsync_WhenGenerateSummariesTrue_UpsertsSummaryAndLinksChildren()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        var chat = new FakeChatClient(_ => "Concise section overview.");
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath, chatClient: chat);
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();
        var collectionProvider = scope.ServiceProvider.GetRequiredService<ZVec.Rag.Internal.RagCollectionProvider>();

        const string sourceUri = "fixture://section-summary-doc";
        var options = new IngestOptions
        {
            GenerateSummaries = true,
            SourceUri = sourceUri,
            SummarySectionMaxTokens = 4096
        };

        var result = await ingestor.IngestTextAsync(
            "Alpha beta gamma delta epsilon.",
            "section-doc",
            options,
            TestContext.Current.CancellationToken);

        Assert.True(result.ChunksIngested >= 1);

        var summaryCollection = await collectionProvider.GetSummaryCollectionAsync(TestContext.Current.CancellationToken);
        int summaryCount = 0;
        await foreach (var _ in summaryCollection.GetAsync(
            r => r.SourceDoc == "section-doc",
            10,
            new FilteredRecordRetrievalOptions<ZVecRagSectionSummaryV1> { IncludeVectors = false },
            cancellationToken: TestContext.Current.CancellationToken))
        {
            summaryCount++;
        }

        Assert.Equal(1, summaryCount);

        string expectedSectionSummaryId = ZVecChunkIdGenerator.ComputeSectionSummaryId(sourceUri, sectionIndex: 0);
        string expectedChunkId = ZVecChunkIdGenerator.Compute(sourceUri, ZVecChunkIdGenerator.DefaultStrategyId, chunkIndex: 0);
        Assert.Equal(expectedChunkId, result.ChunkIds[0]);

        var chunkCollection = await collectionProvider.GetCollectionAsync(TestContext.Current.CancellationToken);
        ZVecRagRecordV1? child = null;
        await foreach (var record in chunkCollection.GetAsync(
            r => r.SourceDoc == "section-doc",
            10,
            new FilteredRecordRetrievalOptions<ZVecRagRecordV1> { IncludeVectors = false },
            cancellationToken: TestContext.Current.CancellationToken))
        {
            if (record.ChunkId == expectedChunkId)
            {
                child = record;
                break;
            }
        }

        Assert.NotNull(child);
        Assert.Equal(expectedSectionSummaryId, child.SectionSummaryId);
    }

    [Fact]
    public async Task IngestTextAsync_OnDuplicateReplace_DeletesPriorSummariesForSourceDoc()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        string firstSummaryId = ZVecChunkIdGenerator.ComputeSectionSummaryId("replace-doc", 0);
        var summaryOptions = new IngestOptions
        {
            GenerateSummaries = true,
            SummarySectionMaxTokens = 4096
        };

        await using (var provider = RagTestHarness.CreateServiceProvider(
            storagePath,
            chatClient: new FakeChatClient(_ => "First summary pass.")))
        {
            using var scope = provider.CreateScope();
            var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();
            await ingestor.IngestTextAsync("first version text", "replace-doc", summaryOptions, TestContext.Current.CancellationToken);
        }

        await using var provider2 = RagTestHarness.CreateServiceProvider(
            storagePath,
            chatClient: new FakeChatClient(_ => "Second summary pass."),
            modelId: "test-model-v1");
        using var scope2 = provider2.CreateScope();
        var ingestor2 = scope2.ServiceProvider.GetRequiredService<IRagIngestor>();
        var collectionProvider = scope2.ServiceProvider.GetRequiredService<ZVec.Rag.Internal.RagCollectionProvider>();

        await ingestor2.IngestTextAsync(
            "replacement text version",
            "replace-doc",
            new IngestOptions { GenerateSummaries = true, OnDuplicate = DuplicateMode.Replace, SummarySectionMaxTokens = 4096 },
            TestContext.Current.CancellationToken);

        var summaryCollection = await collectionProvider.GetSummaryCollectionAsync(TestContext.Current.CancellationToken);
        var summaryIds = new List<string>();
        await foreach (var record in summaryCollection.GetAsync(
            r => r.SourceDoc == "replace-doc",
            10,
            new FilteredRecordRetrievalOptions<ZVecRagSectionSummaryV1> { IncludeVectors = false },
            cancellationToken: TestContext.Current.CancellationToken))
        {
            summaryIds.Add(record.SectionSummaryId);
        }

        Assert.Single(summaryIds);
        ZVecRagSectionSummaryV1? stored = null;
        await foreach (var record in summaryCollection.GetAsync(
            r => r.SourceDoc == "replace-doc",
            1,
            new FilteredRecordRetrievalOptions<ZVecRagSectionSummaryV1> { IncludeVectors = false },
            cancellationToken: TestContext.Current.CancellationToken))
        {
            stored = record;
        }

        Assert.NotNull(stored);
        Assert.Equal("Second summary pass.", stored.Summary);
        Assert.Equal(firstSummaryId, stored.SectionSummaryId);
    }

    [Fact]
    public async Task IngestTextAsync_WhenCanceledMidSummarize_DoesNotUpsertCanceledSectionChildren()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        var chat = new CancelOnNthSummarizeChatClient(failOnCall: 2);
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath, chatClient: chat);
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();

        string sectionText = string.Join(' ', Enumerable.Range(0, 200).Select(i => $"token{i}"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await ingestor.IngestTextAsync(
                sectionText,
                "cancel-doc",
                new IngestOptions
                {
                    GenerateSummaries = true,
                    SummarySectionMaxTokens = 32
                },
                TestContext.Current.CancellationToken));

        var collectionProvider = scope.ServiceProvider.GetRequiredService<ZVec.Rag.Internal.RagCollectionProvider>();
        var chunkCollection = await collectionProvider.GetCollectionAsync(TestContext.Current.CancellationToken);
        int chunkCount = 0;
        await foreach (var _ in chunkCollection.GetAsync(
            r => r.SourceDoc == "cancel-doc",
            100,
            new FilteredRecordRetrievalOptions<ZVecRagRecordV1> { IncludeVectors = false },
            cancellationToken: TestContext.Current.CancellationToken))
        {
            chunkCount++;
        }

        Assert.True(chunkCount < 200);
    }

    private sealed class CancelOnNthSummarizeChatClient : IChatClient
    {
        private int _calls;
        private readonly int _failOnCall;

        public CancelOnNthSummarizeChatClient(int failOnCall) => _failOnCall = failOnCall;

        public ChatClientMetadata Metadata { get; } = new("cancel-on-nth-chat");

        public TService? GetService<TService>(object? key = null) where TService : class => null;

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            _calls++;
            if (_calls >= _failOnCall)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "section summary")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
