using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Schema;
using ZVec.Rag.Testing;
using ZVec.Rag.Testing.Evaluation;

namespace ZVec.Rag.Tests.Ingestion;

public sealed class ArabicFixtureRetrieveTests
{
    [Fact]
    public async Task RetrieveAsync_ArabicQuery_ReturnsArFaqSourceDoc()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        var services = new ServiceCollection();
        services.AddZVecRag(opts =>
        {
            opts.StoragePath = storagePath;
            opts.Embedder = new SemanticTestEmbedder(ZVecRagRecordV1.DefaultDimensions);
            opts.Chat = new FakeChatClient("ok");
            opts.VectorStore.ModelId = "arabic-fixture";
        }).AddTokenChunker();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();
        var retriever = scope.ServiceProvider.GetRequiredService<IRagRetriever>();

        string arPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample02", "ar-faq.txt");
        string enPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample02", "en-faq.txt");
        string arText = await File.ReadAllTextAsync(arPath, TestContext.Current.CancellationToken);
        string enText = await File.ReadAllTextAsync(enPath, TestContext.Current.CancellationToken);

        await ingestor.IngestTextAsync(arText, "ar-faq.txt", cancellationToken: TestContext.Current.CancellationToken);
        await ingestor.IngestTextAsync(enText, "en-faq.txt", cancellationToken: TestContext.Current.CancellationToken);

        var citations = await retriever.RetrieveAsync("زفيك", topK: 5, TestContext.Current.CancellationToken);

        Assert.Contains(citations, c => c.SourceDoc == "ar-faq.txt");
    }
}
