using ZVec.Rag.Constants;

namespace ZVec.Rag.Ingestion;

/// <summary>
/// Validates PDF magic bytes against declared content type before format parsing.
/// </summary>
public static class PdfMagicSniffer
{
    private static readonly byte[] PdfMagicPrefix = "%PDF-"u8.ToArray();

    /// <summary>
    /// Rejects when declared content type does not match the stream's PDF magic prefix.
    /// </summary>
    public static void Validate(Stream documentStream, string contentType)
    {
        if (documentStream == null)
        {
            throw new ArgumentNullException(nameof(documentStream));
        }

        bool isPdfMagic = HasPdfMagicPrefix(documentStream);
        bool claimedPdf = contentType.Equals(ZVecRagConstants.PdfContentType, StringComparison.OrdinalIgnoreCase);

        if (claimedPdf && !isPdfMagic)
        {
            throw new ArgumentException(
                ZVecRagErrorMessages.ContentTypeMagicMismatch(contentType),
                nameof(contentType));
        }

        if (!claimedPdf && isPdfMagic)
        {
            throw new ArgumentException(
                ZVecRagErrorMessages.ContentTypeMagicMismatch(contentType),
                nameof(contentType));
        }
    }

    private static bool HasPdfMagicPrefix(Stream documentStream)
    {
        if (!documentStream.CanSeek)
        {
            throw new NotSupportedException("Document stream must be seekable to validate PDF magic.");
        }

        long originalPosition = documentStream.Position;
        try
        {
            var buffer = new byte[5];
            int read = documentStream.Read(buffer, 0, buffer.Length);
            if (read < PdfMagicPrefix.Length)
            {
                return false;
            }

            for (int i = 0; i < PdfMagicPrefix.Length; i++)
            {
                if (buffer[i] != PdfMagicPrefix[i])
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            documentStream.Position = originalPosition;
        }
    }
}
