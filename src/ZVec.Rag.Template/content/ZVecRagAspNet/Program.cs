using Microsoft.Extensions.AI;
using ZVec.Rag.Schema;
using ZVec.Rag.Streaming;
using ZVec.Rag.Testing;
#if (llm == "llamasharp")
using ZVec.Rag.LLamaSharp;
#endif
#if (embedder == "onnx")
using ZVec.Rag.ONNX;
#endif

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddZVecRag(opts =>
{
    opts.StoragePath = "./rag.zvec";
    opts.Embedder = embedder;
    opts.Chat = chat;
})
.AddTokenChunker();

var app = builder.Build();

app.MapPost("/ingest", async (string text, string docId, ZVec.Rag.Abstractions.IRagIngestor ingestor) =>
{
    await ingestor.IngestTextAsync(text, docId);
    return Results.Ok($"Document {docId} ingested.");
});

app.MapRagSseEndpoint("/chat");

app.Run();
