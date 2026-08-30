using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;
using ZVec.Rag.Testing;

namespace ZVec.Rag.Pdf.Tests;

public sealed class AddZVecRagPdfTests
{
    [Fact]
    public async Task IngestDocumentAsync_Pdf_Succeeds_AfterAddZVecRagPdf()
    {
        string storagePath = Path.Combine(Path.GetTempPath(), "ZVecPdfTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storagePath);

        try
        {
            byte[] pdfBytes = PdfTestFixtures.CreateOnePagePdf("HELLO-ZVEC-PDF");
            using var pdfStream = new MemoryStream(pdfBytes);

            var services = new ServiceCollection();
            services.AddZVecRag(opts =>
            {
                opts.StoragePath = storagePath;
                opts.Embedder = new DeterministicEmbedder();
                opts.Chat = new FakeChatClient("ZVec", " is local-first.");
            })
            .AddTokenChunker()
            .AddZVecRagPdf();

            await using ServiceProvider provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();

            var result = await ingestor.IngestDocumentAsync(
                pdfStream,
                "pdf-doc-1",
                ZVecRagConstants.PdfContentType,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.ChunksIngested >= 1);
        }
        finally
        {
            try { Directory.Delete(storagePath, recursive: true); } catch { }
        }
    }
}
