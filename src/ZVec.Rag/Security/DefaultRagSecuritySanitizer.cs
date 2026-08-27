using ZVec.Rag.Constants;

namespace ZVec.Rag.Security;

/// <summary>
/// Default sanitizer: escapes delimiter and chunk-marker breakout sequences in untrusted text.
/// Does not remove legitimate document content (e.g. legal/security prose).
/// </summary>
public sealed class DefaultRagSecuritySanitizer : IRagSecuritySanitizer
{
    /// <inheritdoc />
    public string SanitizeChunk(string chunkText)
    {
        if (string.IsNullOrEmpty(chunkText))
        {
            return string.Empty;
        }

        string sanitized = chunkText
            .Replace(ZVecRagConstants.RetrievedContextOpenTag, ZVecRagConstants.EscapedRetrievedContextOpenTag, StringComparison.Ordinal)
            .Replace(ZVecRagConstants.RetrievedContextCloseTag, ZVecRagConstants.EscapedRetrievedContextCloseTag, StringComparison.Ordinal)
            .Replace("[chunk id=\"", ZVecRagConstants.EscapedChunkIdMarkerPrefix, StringComparison.Ordinal);

        return sanitized;
    }
}
