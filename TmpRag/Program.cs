using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Testing;

var storagePath = Path.Combine(Path.GetTempPath(), "TmpRag", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(storagePath);

var services = new ServiceCollection();
services.AddZVecRag(opts =>
{
    opts.StoragePath = storagePath;
    opts.Embedder = new DeterministicEmbedder();
    opts.Chat = new FakeChatClient("Hello", " from ZVec.Rag");
})
.AddTokenChunker();

await using ServiceProvider provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var pipeline = scope.ServiceProvider.GetRequiredService<IRagPipeline>();

string fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "micro-100.txt");
string fixtureText = await File.ReadAllTextAsync(fixturePath);
await pipeline.IngestTextAsync(fixtureText, "micro-100.txt");

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
