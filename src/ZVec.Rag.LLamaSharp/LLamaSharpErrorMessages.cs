namespace ZVec.Rag.LLamaSharp;

/// <summary>
/// Strongly-typed error message templates for ZVec.Rag.LLamaSharp.
/// </summary>
public static class LLamaSharpErrorMessages
{
    /// <summary>Formats error when model path is missing.</summary>
    public static string ModelPathRequired() =>
        "LLamaSharpOptions.ModelPath is required. Set ZVEC_LLAMA_MODEL or provide a GGUF file path.";

    /// <summary>Formats error when the session has been disposed.</summary>
    public static string SessionDisposed() =>
        "LLamaSharp session has been disposed.";

    /// <summary>Formats error when embed input is empty.</summary>
    public static string EmptyEmbedInput() =>
        "Embed input cannot be null or empty.";

    /// <summary>Formats error when LLamaSharp returns no embedding vectors.</summary>
    public static string NoEmbeddingsReturned() =>
        "LLamaSharp returned no embeddings.";

    /// <summary>Formats error when embedding dimension does not match the default RAG record.</summary>
    public static string EmbeddingDimensionMismatch(int actual, int expected) =>
        $"LLamaSharp embedding dimension {actual} does not match ZVecRagRecordV1.DefaultDimensions ({expected}).";
}
