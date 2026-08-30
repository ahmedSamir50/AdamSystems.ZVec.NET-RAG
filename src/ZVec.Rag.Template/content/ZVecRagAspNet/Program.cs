using ZVec.Rag.Abstractions;
using ZVec.Rag.Streaming;
using ZVec.Rag.Testing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddZVecRag(opts =>
{
    opts.StoragePath = "./rag.zvec";
    opts.Embedder = new DeterministicEmbedder();
    // Test double for CI and first run. Concatenates tokens; does not call a model. Replace with your IChatClient (Story 4.1).
    opts.Chat = new FakeChatClient("Hello", " from ZVec.Rag");
})
.AddTokenChunker();

var app = builder.Build();

app.MapPost("/ingest", async (string text, string docId, IRagIngestor ingestor) =>
{
    await ingestor.IngestTextAsync(text, docId);
    return Results.Ok($"Document {docId} ingested.");
});

app.MapRagSseEndpoint("/chat");

app.Run();
