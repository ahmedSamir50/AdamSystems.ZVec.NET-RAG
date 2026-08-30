using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Testing;

var storagePath = Path.Combine(Path.GetTempPath(), "ZVecSample01", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(storagePath);

var services = new ServiceCollection();
services.AddZVecRag(opts =>
{
    opts.StoragePath = storagePath;
    opts.Embedder = new DeterministicEmbedder();
    // Test double for CI and first run. Concatenates tokens; does not call a model. Replace with your IChatClient (Story 4.1).
    opts.Chat = new FakeChatClient("ZVec", " is local-first.");
})
.AddTokenChunker();

await using ServiceProvider provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var pipeline = scope.ServiceProvider.GetRequiredService<IRagPipeline>();

string helloPath = Path.Combine(AppContext.BaseDirectory, "docs", "hello.md");
string hello = await File.ReadAllTextAsync(helloPath);
await pipeline.IngestTextAsync(hello, "hello.md");

bool printedCitation = false;
await foreach (var chunk in pipeline.AskAsync("What is ZVec?"))
{
    Console.Write(chunk.Text);
    if (!printedCitation && chunk.Citations.Count > 0)
    {
        var c = chunk.Citations[0];
        Console.WriteLine();
        Console.WriteLine($"Citation: {c.ChunkId} from {c.SourceDoc}");
        printedCitation = true;
    }
}
