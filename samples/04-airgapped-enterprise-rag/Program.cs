using Microsoft.Extensions.AI;
using ZVec.Rag.Streaming;
using ZVec.Rag.Testing;

var builder = WebApplication.CreateBuilder(args);

// Test double for CI and first run. Concatenates tokens; does not call a model. Replace with your IChatClient (Story 4.1).
IChatClient chat = new FakeChatClient("Enterprise", " RAG ready.");

builder.Services.AddZVecRag(opts =>
{
    opts.StoragePath = "./enterprise-rag.zvec";
    opts.Embedder = new DeterministicEmbedder();
    opts.Chat = chat;
})
.AddTokenChunker();

var app = builder.Build();

app.MapRagSseEndpoint("/chat");
app.Run();
