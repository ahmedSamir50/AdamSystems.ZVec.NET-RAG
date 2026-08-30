using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Schema;
using ZVec.Rag.Testing;

namespace ZVec.Rag.Benchmarks;

/// <summary>
/// Local allocation benchmarks for hybrid retrieval over a small deterministic corpus.
/// </summary>
[MemoryDiagnoser]
public class QueryAllocationBenchmarks
{
    private ServiceProvider? _provider;
    private IServiceScope? _retrieveScope;
    private IRagRetriever? _retriever;
    private string _storagePath = string.Empty;

    [GlobalSetup]
    public async Task Setup()
    {
        _storagePath = Path.Combine(Path.GetTempPath(), "ZVecBench", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_storagePath);

        var services = new ServiceCollection();
        services.AddZVecRag(opts =>
        {
            opts.StoragePath = _storagePath;
            opts.Embedder = new DeterministicEmbedder(ZVecRagRecordV1.DefaultDimensions);
            opts.Chat = new FakeChatClient("ok");
            opts.VectorStore.ModelId = "bench-model";
            opts.RetrieveTopK = 5;
        })
        .AddTokenChunker(maxTokens: 64, overlapTokens: 8);

        _provider = services.BuildServiceProvider();
        using var scope = _provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();

        for (int i = 0; i < 256; i++)
        {
            await ingestor.IngestTextAsync(
                $"Benchmark chunk {i}: local vector retrieval over embedded text corpus.",
                $"bench-doc-{i % 8}",
                cancellationToken: CancellationToken.None);
        }

        _retrieveScope = _provider.CreateScope();
        _retriever = _retrieveScope.ServiceProvider.GetRequiredService<IRagRetriever>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _retrieveScope?.Dispose();
        _provider?.Dispose();
        try
        {
            Directory.Delete(_storagePath, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Benchmark]
    public async Task RetrieveAsync_256ChunkCorpus()
    {
        _ = await _retriever!.RetrieveAsync(
            "local vector retrieval embedded corpus",
            topK: 5,
            cancellationToken: CancellationToken.None);
    }
}

internal static class Program
{
    private static void Main(string[] args)
        => BenchmarkRunner.Run<QueryAllocationBenchmarks>(args: args);
}
