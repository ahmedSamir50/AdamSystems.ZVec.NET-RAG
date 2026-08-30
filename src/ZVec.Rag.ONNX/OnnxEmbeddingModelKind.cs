namespace ZVec.Rag.ONNX;

/// <summary>
/// Supported ONNX embedding model kinds for <see cref="OnnxEmbedder"/>.
/// </summary>
public enum OnnxEmbeddingModelKind
{
    /// <summary>MiniLM sentence embedding (typically 384 dimensions).</summary>
    MiniLm,

    /// <summary>EmbeddingGemma text embedding (768 dimensions for ZVec pipeline).</summary>
    EmbeddingGemma,

    /// <summary>CLIP text tower (512 dimensions; multimodal record schema).</summary>
    ClipText,
}
