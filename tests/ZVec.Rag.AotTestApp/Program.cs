using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Schema;
using ZVec.Rag.Testing;

namespace ZVec.Rag.AotTestApp;

public static class Program
{
    public static int Main()
    {
        Console.WriteLine("=== ZVec.Rag Pipeline Native AOT Harness Starting ===");

        try
        {
            string storagePath = Path.Combine(Path.GetTempPath(), "ZVecRagAotTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(storagePath);

            var services = new ServiceCollection();
            services.AddZVecRag(opts =>
            {
                opts.StoragePath = storagePath;
                opts.Embedder = new DeterministicEmbedder(ZVecRagRecordV1.DefaultDimensions);
                opts.Chat = new FakeChatClient("AOT", " ok");
                opts.VectorStore.ModelId = "rag-aot-harness-v1";
            })
            .AddTokenChunker(maxTokens: 64, overlapTokens: 8);

            using ServiceProvider provider = services.BuildServiceProvider();
            using IServiceScope scope = provider.CreateScope();
            IRagIngestor ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();
            IRagRetriever retriever = scope.ServiceProvider.GetRequiredService<IRagRetriever>();

            var ingestResult = ingestor.IngestTextAsync(
                "Native AOT harness ingests plain text through bounded channels and Tiktoken chunking.",
                "aot-doc",
                cancellationToken: CancellationToken.None).GetAwaiter().GetResult();

            if (ingestResult.ChunksIngested < 1)
            {
                throw new InvalidOperationException("IngestTextAsync produced zero chunks under AOT.");
            }

            Console.WriteLine($"[AOT Test 1] IngestTextAsync wrote {ingestResult.ChunksIngested} chunk(s).");

            var citations = retriever.RetrieveAsync(
                "AOT harness channels Tiktoken",
                cancellationToken: CancellationToken.None).GetAwaiter().GetResult();

            if (citations.Count == 0)
            {
                throw new InvalidOperationException("RetrieveAsync returned no citations under AOT.");
            }

            Console.WriteLine($"[AOT Test 2] RetrieveAsync returned {citations.Count} citation(s). Top doc={citations[0].SourceDoc}");

            try
            {
                if (Directory.Exists(storagePath))
                {
                    Directory.Delete(storagePath, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup for temp harness storage.
            }

            Console.WriteLine("=== ZVec.Rag Pipeline Native AOT Harness Passed ===");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] RAG AOT harness failure: {ex.GetType().Name} - {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}
