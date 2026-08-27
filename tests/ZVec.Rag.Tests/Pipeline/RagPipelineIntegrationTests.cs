using Microsoft.Extensions.DependencyInjection;
using ZVec.Extensions.VectorData.Store;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Ingestion;
using ZVec.Rag.Testing;

namespace ZVec.Rag.Tests.Pipeline;

/// <summary>
/// Integration tests for the RAG pipeline using the real ZVec connector harness.
/// </summary>
public sealed class RagPipelineIntegrationTests
{
    [Fact]
    public async Task IngestTextAsync_PersistsChunk_WithDeterministicChunkId()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath);
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();

        var result = await ingestor.IngestTextAsync(
            "ZVec is an embedded vector database for .NET.",
            "doc-001",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, result.ChunksIngested);
        Assert.Single(result.ChunkIds);

        string expectedId = ZVecChunkIdGenerator.Compute(
            "doc-001",
            ZVecChunkIdGenerator.DefaultStrategyId,
            0);
        Assert.Equal(expectedId, result.ChunkIds[0]);
    }

    [Fact]
    public async Task RetrieveAsync_ReturnsIngestedCitation_AfterHybridSearch()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath);
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();
        var retriever = scope.ServiceProvider.GetRequiredService<IRagRetriever>();

        await ingestor.IngestTextAsync(
            "Local-first RAG combines retrieval with generation.",
            "rag-doc",
            cancellationToken: TestContext.Current.CancellationToken);

        IReadOnlyList<ZVec.Rag.Models.Citation> citations = await retriever.RetrieveAsync(
            "local-first RAG retrieval generation",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEmpty(citations);
        Assert.Contains(citations, c => c.SourceDoc == "rag-doc");
    }

    [Fact]
    public async Task AskAsync_IncludesConversationHistory_InChatMessages()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        var chat = new FakeChatClient("Answer", " token");
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath, chatClient: chat);
        using var scope = provider.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IRagPipeline>();

        await pipeline.IngestTextAsync(
            "ZVec stores vectors locally.",
            "history-doc",
            cancellationToken: TestContext.Current.CancellationToken);

        var history = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(Microsoft.Extensions.AI.ChatRole.User, "Earlier question"),
            new(Microsoft.Extensions.AI.ChatRole.Assistant, "Earlier answer")
        };

        await foreach (var _ in pipeline.AskAsync(
            "What stores vectors?",
            history,
            cancellationToken: TestContext.Current.CancellationToken))
        {
        }

        Assert.Contains(chat.LastStreamingMessages, m => m.Role == Microsoft.Extensions.AI.ChatRole.User && m.Text == "Earlier question");
        Assert.Contains(chat.LastStreamingMessages, m => m.Role == Microsoft.Extensions.AI.ChatRole.Assistant && m.Text == "Earlier answer");
    }

    [Fact]
    public async Task AskAsync_StreamsFakeChatTokens_WithCitations()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath);
        using var scope = provider.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IRagPipeline>();

        await pipeline.IngestTextAsync(
            "Microsoft.Extensions.AI provides IChatClient and IEmbeddingGenerator.",
            "meai-doc",
            cancellationToken: TestContext.Current.CancellationToken);

        var chunks = new List<ZVec.Rag.Models.RagChunk>();
        await foreach (var chunk in pipeline.AskAsync(
            "What does Microsoft.Extensions.AI provide?",
            cancellationToken: TestContext.Current.CancellationToken))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);
        Assert.Contains(chunks, c => c.Text.Contains("Answer", StringComparison.Ordinal));
        Assert.Contains(chunks, c => c.Citations.Any(ct => ct.SourceDoc == "meai-doc"));
        Assert.Contains(chunks, c => c.IsFinal);
    }

    [Fact]
    public async Task IngestTextAsync_ThrowsOperationCanceledException_WhenTokenCanceled()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath);
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await ingestor.IngestTextAsync("text", "doc", cancellationToken: cts.Token));
    }

    [Fact]
    public async Task SecondPipelineOpen_ThrowsZVecRagInitializationException_WhenModelIdMismatch()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        await using (var provider = RagTestHarness.CreateServiceProvider(storagePath, modelId: "model-a"))
        {
            using var scope = provider.CreateScope();
            var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();
            await ingestor.IngestTextAsync("seed", "doc", cancellationToken: TestContext.Current.CancellationToken);
        }

        await using var mismatchProvider = RagTestHarness.CreateServiceProvider(storagePath, modelId: "model-b");
        using var mismatchScope = mismatchProvider.CreateScope();
        var retriever = mismatchScope.ServiceProvider.GetRequiredService<IRagRetriever>();

        var ex = await Assert.ThrowsAsync<ZVec.Rag.Exceptions.ZVecRagInitializationException>(async () =>
            await retriever.RetrieveAsync("seed", cancellationToken: TestContext.Current.CancellationToken));

        Assert.IsType<ZVec.Extensions.VectorData.Manifest.ZVecEmbedderMismatchException>(ex.InnerException);
        Assert.Contains("IRagMigrationManager", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddZVecRag_RegistersScopedPipeline_AndSingletonStore()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        var services = new ServiceCollection();
        services.AddZVecRag(opts =>
        {
            opts.StoragePath = storagePath;
            opts.Embedder = new DeterministicEmbedder();
            opts.Chat = new FakeChatClient("x");
            opts.VectorStore.EnableMmap = false;
            opts.VectorStore.MaxConcurrentNativeCalls = 2;
        })
        .AddTokenChunker();

        using var provider = services.BuildServiceProvider();
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        Assert.NotSame(scope1.ServiceProvider.GetRequiredService<IRagPipeline>(), scope2.ServiceProvider.GetRequiredService<IRagPipeline>());
        Assert.Same(provider.GetRequiredService<ZVecVectorStore>(), scope1.ServiceProvider.GetRequiredService<ZVecVectorStore>());

        var options = provider.GetRequiredService<ZVecVectorStoreOptions>();
        Assert.Equal(storagePath, options.StoragePath);
        Assert.False(options.EnableMmap);
        Assert.Equal(2, options.MaxConcurrentNativeCalls);
    }
}
