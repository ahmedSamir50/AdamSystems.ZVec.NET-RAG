using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Schema;
using ZVec.Rag.Testing;
#if (llm == "llamasharp")
using ZVec.Rag.LLamaSharp;
#endif
#if (embedder == "onnx")
using ZVec.Rag.ONNX;
#endif

var storagePath = Path.Combine(Path.GetTempPath(), "ZVecRagApp", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(storagePath);

IChatClient chat = new FakeChatClient("Hello", " from ZVec.Rag");
#if (llm == "llamasharp")
string? llamaModelPath = Environment.GetEnvironmentVariable("ZVEC_LLAMA_MODEL");
if (!string.IsNullOrWhiteSpace(llamaModelPath))
{
    chat = new LLamaSharpChatClient(new LLamaSharpOptions { ModelPath = llamaModelPath });
}
#endif

IEmbeddingGenerator<string, Embedding<float>> embedder =
#if (embedder == "onnx")
    Environment.GetEnvironmentVariable("ZVEC_ONNX_MODEL") is { Length: > 0 } onnxPath
        ? new OnnxEmbedder(new OnnxEmbedderOptions
        {
            ModelPath = onnxPath,
            ModelKind = OnnxEmbeddingModelKind.EmbeddingGemma,
            Dimensions = ZVecRagRecordV1.DefaultDimensions
        })
        : new DeterministicEmbedder();
#else
    new DeterministicEmbedder();
#endif

var services = new ServiceCollection();
services.AddZVecRag(opts =>
{
    opts.StoragePath = storagePath;
    opts.Embedder = embedder;
    opts.Chat = chat;
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
