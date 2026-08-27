using System.Text;
using ZVec.Rag.Abstractions;

namespace ZVec.Rag.Ingestion;

/// <summary>
/// UTF-8 plain text and markdown stream reader.
/// </summary>
public sealed class PlainTextDocumentReader : IRagDocumentReader
{
    /// <inheritdoc />
    public async ValueTask<string> ReadAsync(Stream documentStream, CancellationToken cancellationToken = default)
    {
        if (documentStream == null)
        {
            throw new ArgumentNullException(nameof(documentStream));
        }

        using var reader = new StreamReader(
            documentStream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }
}
