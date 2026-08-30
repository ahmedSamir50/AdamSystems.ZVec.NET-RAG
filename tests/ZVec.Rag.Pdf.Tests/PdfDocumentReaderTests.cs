using ZVec.Rag.Constants;
using ZVec.Rag.Pdf;

namespace ZVec.Rag.Pdf.Tests;

public sealed class PdfDocumentReaderTests
{
    [Fact]
    public async Task ExtractsHelloSentence_FromOnePagePdf()
    {
        byte[] pdfBytes = PdfTestFixtures.CreateOnePagePdf("HELLO-ZVEC-PDF");
        using var stream = new MemoryStream(pdfBytes);

        var reader = new PdfDocumentReader();
        string text = await reader.ReadAsync(
            stream,
            ZVecRagConstants.PdfContentType,
            TestContext.Current.CancellationToken);

        Assert.Contains("HELLO-ZVEC-PDF", text);
    }

    [Fact]
    public async Task EmptyPdf_ThrowsNullOrEmptyText()
    {
        byte[] pdfBytes = PdfTestFixtures.CreateOnePagePdf(string.Empty);
        using var stream = new MemoryStream(pdfBytes);

        var reader = new PdfDocumentReader();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            reader.ReadAsync(
                stream,
                ZVecRagConstants.PdfContentType,
                TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(ZVecRagErrorMessages.NullOrEmptyText(), ex.Message);
    }
}
