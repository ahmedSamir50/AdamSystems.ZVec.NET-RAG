using ZVec.Rag.Constants;
using ZVec.Rag.Ingestion;

namespace ZVec.Rag.Tests.Ingestion;

/// <summary>
/// Tests for <see cref="IRagDocumentReader"/> content-type dispatch.
/// </summary>
public sealed class IRagDocumentReaderTests
{
    [Fact]
    public async Task ReadAsync_Utf8PlainText_RoundTrips()
    {
        var reader = new PlainTextDocumentReader();
        using var stream = new MemoryStream("hello markdown"u8.ToArray());

        string result = await reader.ReadAsync(
            stream,
            ZVecRagConstants.PlainTextContentType,
            TestContext.Current.CancellationToken);

        Assert.Equal("hello markdown", result);
    }

    [Fact]
    public async Task ReadAsync_ApplicationPdf_ThrowsPdfPackageRequired()
    {
        var reader = new PlainTextDocumentReader();
        using var stream = new MemoryStream("fake pdf"u8.ToArray());

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            reader.ReadAsync(
                stream,
                ZVecRagConstants.PdfContentType,
                TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(ZVecRagErrorMessages.PdfPackageRequired(), ex.Message);
    }
}
