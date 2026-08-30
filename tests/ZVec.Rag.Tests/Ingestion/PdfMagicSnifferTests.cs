using ZVec.Rag.Constants;
using ZVec.Rag.Ingestion;

namespace ZVec.Rag.Tests.Ingestion;

/// <summary>
/// Tests for <see cref="PdfMagicSniffer"/>.
/// </summary>
public sealed class PdfMagicSnifferTests
{
    [Fact]
    public void ClaimedPdf_WithoutPercentPdfPrefix_ThrowsMismatch()
    {
        using var stream = new MemoryStream("not a pdf"u8.ToArray());

        var ex = Assert.Throws<ArgumentException>(() =>
            PdfMagicSniffer.Validate(stream, ZVecRagConstants.PdfContentType));

        Assert.StartsWith(ZVecRagErrorMessages.ContentTypeMagicMismatch(ZVecRagConstants.PdfContentType), ex.Message);
        Assert.Equal("contentType", ex.ParamName);
    }

    [Fact]
    public void ClaimedText_WithPercentPdfPrefix_ThrowsMismatch()
    {
        using var stream = new MemoryStream("%PDF-1.4 fake"u8.ToArray());

        var ex = Assert.Throws<ArgumentException>(() =>
            PdfMagicSniffer.Validate(stream, ZVecRagConstants.PlainTextContentType));

        Assert.StartsWith(ZVecRagErrorMessages.ContentTypeMagicMismatch(ZVecRagConstants.PlainTextContentType), ex.Message);
        Assert.Equal("contentType", ex.ParamName);
    }

    [Fact]
    public void ClaimedPdf_WithPercentPdfPrefix_DoesNotThrow()
    {
        using var stream = new MemoryStream("%PDF-1.4"u8.ToArray());

        PdfMagicSniffer.Validate(stream, ZVecRagConstants.PdfContentType);

        Assert.Equal(0, stream.Position);
    }
}
