using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;

namespace ZVec.Rag.Pdf;

/// <summary>
/// Routes document reads to text or PDF readers based on content type.
/// </summary>
public sealed class CompositeRagDocumentReader : IRagDocumentReader
{
    private readonly IRagDocumentReader _textReader;
    private readonly IRagDocumentReader _pdfReader;

    /// <summary>Creates a composite reader over text and PDF implementations.</summary>
    public CompositeRagDocumentReader(IRagDocumentReader textReader, IRagDocumentReader pdfReader)
    {
        _textReader = textReader ?? throw new ArgumentNullException(nameof(textReader));
        _pdfReader = pdfReader ?? throw new ArgumentNullException(nameof(pdfReader));
    }

    /// <inheritdoc />
    public ValueTask<string> ReadAsync(
        Stream documentStream,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (contentType.Equals(ZVecRagConstants.PdfContentType, StringComparison.OrdinalIgnoreCase))
        {
            return _pdfReader.ReadAsync(documentStream, contentType, cancellationToken);
        }

        return _textReader.ReadAsync(documentStream, contentType, cancellationToken);
    }
}
