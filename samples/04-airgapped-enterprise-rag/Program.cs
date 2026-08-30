using Microsoft.Extensions.AI;
using ZVec.Rag.LLamaSharp;
using ZVec.Rag.Streaming;
using ZVec.Rag.Testing;

var builder = WebApplication.CreateBuilder(args);

IChatClient chat = new FakeChatClient("Enterprise", " RAG ready.");
IEmbeddingGenerator<string, Embedding<float>> embedder = new DeterministicEmbedder();

string? llamaModelPath = Environment.GetEnvironmentVariable("ZVEC_LLAMA_MODEL");
if (!string.IsNullOrWhiteSpace(llamaModelPath))
{
    var llamaOptions = new LLamaSharpOptions { ModelPath = llamaModelPath };
    chat = new LLamaSharpChatClient(llamaOptions);

    string? embedFlag = Environment.GetEnvironmentVariable("ZVEC_LLAMA_EMBED");
    if (string.Equals(embedFlag, "1", StringComparison.Ordinal))
    {
        embedder = new LLamaSharpEmbedder(llamaOptions);
    }
}

builder.Services.AddZVecRag(opts =>
{
    opts.StoragePath = "./enterprise-rag.zvec";
    opts.Embedder = embedder;
    opts.Chat = chat;
})
.AddTokenChunker();

var app = builder.Build();

app.MapRagSseEndpoint("/chat");
app.Run();
