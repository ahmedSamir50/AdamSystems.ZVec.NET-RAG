using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;

namespace ZVec.Rag.Tests.Ingestion;

/// <summary>
/// Ingest-level tests for PDF magic-byte validation.
/// </summary>
public sealed class RagIngestorMagicMismatchTests
{
    [Fact]
    public async Task IngestDocumentAsync_ClaimedPdf_PlainBytes_ThrowsMismatch()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);

        try
        {
            await using var provider = RagTestHarness.CreateServiceProvider(storagePath);
            using var scope = provider.CreateScope();
            var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();

            using var stream = new MemoryStream("hello"u8.ToArray());

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                ingestor.IngestDocumentAsync(
                    stream,
                    "doc-1",
                    ZVecRagConstants.PdfContentType,
                    cancellationToken: TestContext.Current.CancellationToken).AsTask());

            Assert.StartsWith(ZVecRagErrorMessages.ContentTypeMagicMismatch(ZVecRagConstants.PdfContentType), ex.Message);
            Assert.Equal("contentType", ex.ParamName);
        }
        finally
        {
            try { Directory.Delete(storagePath, recursive: true); } catch { }
        }
    }
}
