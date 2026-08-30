using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Models;
using ZVec.Rag.Schema;
using ZVec.Rag.Testing;

namespace ZVec.Rag.AotTestApp;

public static class Program
{
    public static async Task<int> Main()
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

            await using ServiceProvider provider = services.BuildServiceProvider();
            await using AsyncServiceScope scope = provider.CreateAsyncScope();
            IRagIngestor ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();
            IRagRetriever retriever = scope.ServiceProvider.GetRequiredService<IRagRetriever>();
            IRagGenerator generator = scope.ServiceProvider.GetRequiredService<IRagGenerator>();

            var ingestResult = await ingestor.IngestTextAsync(
                "Native AOT harness ingests plain text through bounded channels and Tiktoken chunking.",
                "aot-doc",
                cancellationToken: CancellationToken.None).ConfigureAwait(false);

            if (ingestResult.ChunksIngested < 1)
            {
                throw new InvalidOperationException("IngestTextAsync produced zero chunks under AOT.");
            }

            Console.WriteLine($"[AOT Test 1] IngestTextAsync wrote {ingestResult.ChunksIngested} chunk(s).");

            var citations = await retriever.RetrieveAsync(
                "AOT harness channels Tiktoken",
                cancellationToken: CancellationToken.None).ConfigureAwait(false);

            if (citations.Count == 0)
            {
                throw new InvalidOperationException("RetrieveAsync returned no citations under AOT.");
            }

            Console.WriteLine($"[AOT Test 2] RetrieveAsync returned {citations.Count} citation(s). Top doc={citations[0].SourceDoc}");

            bool sawNonEmptyChunk = false;
            await foreach (RagChunk chunk in generator.AskAsync(
                               "What does the AOT harness do?",
                               history: null,
                               streamCitations: true,
                               cancellationToken: CancellationToken.None)
                           .ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(chunk.Text))
                {
                    sawNonEmptyChunk = true;
                }
            }

            if (!sawNonEmptyChunk)
            {
                throw new InvalidOperationException("AskAsync produced no non-empty RagChunk under AOT.");
            }

            Console.WriteLine("[AOT Test 3] AskAsync streamed at least one non-empty RagChunk.");

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
