namespace ZVec.Rag.Constants;

/// <summary>
/// Strongly-typed error message templates for ZVec.Rag.
/// </summary>
public static class ZVecRagErrorMessages
{
    /// <summary>Formats error when embedder is not configured.</summary>
    public static string EmbedderNotConfigured() =>
        "ZVecRagOptions.Embedder is not configured. Register an IEmbeddingGenerator<string, Embedding<float>> via AddZVecRag.";

    /// <summary>Formats error when chat client is not configured.</summary>
    public static string ChatClientNotConfigured() =>
        "ZVecRagOptions.Chat is not configured. Register an IChatClient via AddZVecRag.";

    /// <summary>Formats error when document content type is unsupported in core.</summary>
    public static string UnsupportedContentType(string contentType) =>
        $"Content type '{contentType}' is not supported in core ZVec.Rag. Use plain text or markdown, or install ZVec.Rag.Pdf for PDF ingestion.";

    /// <summary>Formats error when text is null or empty.</summary>
    public static string NullOrEmptyText() =>
        "Ingest text cannot be null or empty.";

    /// <summary>Formats error when document id is null or empty.</summary>
    public static string NullOrEmptyDocumentId() =>
        "Document id cannot be null or empty.";

    /// <summary>Formats error when question is null or empty.</summary>
    public static string NullOrEmptyQuestion() =>
        "Question cannot be null or empty.";

    /// <summary>Formats initialization failure wrapping embedder stamp mismatch.</summary>
    public static string InitializationFailed(string storagePath, string innerMessage) =>
        $"ZVec.Rag pipeline initialization failed for storage at '{storagePath}'. {innerMessage}";
}
