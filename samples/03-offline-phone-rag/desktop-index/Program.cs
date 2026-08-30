using Microsoft.Extensions.DependencyInjection;
using ZVec.Extensions.VectorData.Store;
using ZVec.NET;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Ingestion;
using ZVec.Rag.Models;
using ZVec.Rag.Testing;
using ZVec.Rag.Testing.Evaluation;

var storagePath = Path.Combine(Path.GetTempPath(), "ZVecSample03", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(storagePath);

var services = new ServiceCollection();
services.AddZVecRag(opts =>
{
    opts.StoragePath = storagePath;
    opts.Embedder = new SemanticTestEmbedder();
    opts.Chat = new FakeChatClient("ok");
    opts.VectorStore.EnableMmap = true;
    opts.VectorStore.ReadOnly = false;
    opts.VectorStore.DefaultQuantizeType = ZVecQuantizeType.Fp16;
})
.AddTokenChunker(maxTokens: 64, overlapTokens: 8);

await using ServiceProvider provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();

string fixturesDir = Path.Combine(AppContext.BaseDirectory, "fixtures");
await ingestor.IngestTextAsync(
    await File.ReadAllTextAsync(Path.Combine(fixturesDir, "doc-orion.md")),
    "doc-orion",
    new IngestOptions { SourceUri = "fixture://doc-orion.md" });
await ingestor.IngestTextAsync(
    await File.ReadAllTextAsync(Path.Combine(fixturesDir, "doc-zephyr.md")),
    "doc-zephyr",
    new IngestOptions { SourceUri = "fixture://doc-zephyr.md" });

Console.WriteLine($"Built Fp16 mmap index at {storagePath}");
