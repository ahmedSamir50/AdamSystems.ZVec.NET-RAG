using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;

namespace ZVec.Rag.Tests.Ingestion;

/// <summary>
/// Tests for PDF content-type validation without ZVec.Rag.Pdf installed.
/// </summary>
public sealed class RagIngestorContentTypeTests
{
    [Fact]
    public async Task IngestDocumentAsync_ApplicationPdf_WithoutPdfPackage_ThrowsInstallMessage()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);

        try
        {
            await using var provider = RagTestHarness.CreateServiceProvider(storagePath);
            using var scope = provider.CreateScope();
            var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();

            using var stream = new MemoryStream("%PDF-1.4 fake"u8.ToArray());

            var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
                ingestor.IngestDocumentAsync(
                    stream,
                    "doc-1",
                    ZVecRagConstants.PdfContentType,
                    cancellationToken: TestContext.Current.CancellationToken).AsTask());

            Assert.Equal(ZVecRagErrorMessages.PdfPackageRequired(), ex.Message);
        }
        finally
        {
            try { Directory.Delete(storagePath, recursive: true); } catch { }
        }
    }
}
