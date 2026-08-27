namespace ZVec.Rag.Abstractions;

/// <summary>
/// Reads document bytes into UTF-8 text (format parsing ACL).
/// </summary>
public interface IRagDocumentReader
{
    /// <summary>Reads a document stream into text.</summary>
    ValueTask<string> ReadAsync(Stream documentStream, CancellationToken cancellationToken = default);
}
