namespace ZVec.Rag.Security;

/// <summary>
/// No-op sanitizer for trusted corpora where delimiter escaping is unnecessary.
/// </summary>
public sealed class NoOpRagSecuritySanitizer : IRagSecuritySanitizer
{
    /// <inheritdoc />
    public string SanitizeChunk(string chunkText) => chunkText ?? string.Empty;
}
