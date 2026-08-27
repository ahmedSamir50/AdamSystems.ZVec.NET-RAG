namespace ZVec.Rag.Security;

/// <summary>
/// Defines a security sanitizer for untrusted retrieved RAG chunk text before prompt composition.
/// </summary>
public interface IRagSecuritySanitizer
{
    /// <summary>
    /// Sanitizes retrieved document text so delimiter breakout and forged chunk markers cannot escape the context block.
    /// </summary>
    /// <param name="chunkText">Raw chunk text from retrieval.</param>
    /// <returns>Sanitized text safe to embed inside <c>&lt;retrieved_context&gt;</c>.</returns>
    string SanitizeChunk(string chunkText);
}
