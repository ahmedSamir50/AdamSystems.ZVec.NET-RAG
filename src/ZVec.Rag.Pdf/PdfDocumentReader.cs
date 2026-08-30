using UglyToad.PdfPig;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;

namespace ZVec.Rag.Pdf;

/// <summary>
/// Extracts plain text from PDF streams using PdfPig (text only; no table parsing).
/// </summary>
[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("PdfPig text extraction is not trim-safe for Native AOT.")]
public sealed class PdfDocumentReader : IRagDocumentReader
{
    /// <inheritdoc />
    public ValueTask<string> ReadAsync(
        Stream documentStream,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (documentStream == null)
        {
            throw new ArgumentNullException(nameof(documentStream));
        }

        if (!contentType.Equals(ZVecRagConstants.PdfContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException();
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var document = PdfDocument.Open(documentStream);
        var pages = new List<string>(document.NumberOfPages);

        for (int pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = document.GetPage(pageNumber);
            pages.Add(page.Text);
        }

        string text = string.Join('\n', pages);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(ZVecRagErrorMessages.NullOrEmptyText());
        }

        return ValueTask.FromResult(text);
    }
}
