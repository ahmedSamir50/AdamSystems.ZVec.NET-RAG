using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Schema;
using ZVec.Rag.Testing;

namespace ZVec.Rag.Tests;

/// <summary>
/// Shared harness for RAG integration tests (real ZVec + deterministic fakes).
/// </summary>
internal static class RagTestHarness
{
    internal static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "ZVecTests", Guid.NewGuid().ToString("N"));

    internal static ServiceProvider CreateServiceProvider(
        string storagePath,
        FakeChatClient? chatClient = null,
        string modelId = "test-model-v1")
    {
        var chat = chatClient ?? new FakeChatClient("Answer", " token");
        var services = new ServiceCollection();
        services.AddZVecRag(opts =>
        {
            opts.StoragePath = storagePath;
            opts.Embedder = new DeterministicEmbedder(ZVecRagRecordV1.DefaultDimensions);
            opts.Chat = chat;
            opts.VectorStore.ModelId = modelId;
            opts.RetrieveTopK = 3;
        })
        .AddTokenChunker()
        .AddMarkdownChunker()
        .AddSentenceChunker();

        return services.BuildServiceProvider();
    }
}
