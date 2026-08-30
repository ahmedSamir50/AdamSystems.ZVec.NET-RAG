using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Models;
using ZVec.Rag.Schema;
using ZVec.Rag.Testing;
using ZVec.Rag.Testing.Evaluation;

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
        IChatClient? chatClient = null,
        string modelId = "test-model-v1",
        bool generateSummaries = false,
        IEmbeddingGenerator<string, Embedding<float>>? embedder = null,
        int retrieveTopK = 3)
    {
        var chat = chatClient ?? new FakeChatClient("Answer", " token");
        var services = new ServiceCollection();
        services.AddZVecRag(opts =>
        {
            opts.StoragePath = storagePath;
            opts.Embedder = embedder ?? new DeterministicEmbedder(ZVecRagRecordV1.DefaultDimensions);
            opts.Chat = chat;
            opts.VectorStore.ModelId = modelId;
            opts.RetrieveTopK = retrieveTopK;
            opts.GenerateSummaries = generateSummaries;
        })
        .AddTokenChunker()
        .AddMarkdownChunker()
        .AddSentenceChunker();

        return services.BuildServiceProvider();
    }

    internal static IEmbeddingGenerator<string, Embedding<float>> CreateSemanticEmbedder()
        => new SemanticTestEmbedder(ZVecRagRecordV1.DefaultDimensions);
}
