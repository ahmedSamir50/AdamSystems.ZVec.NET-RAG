namespace ZVec.Rag.ONNX;

/// <summary>
/// Configuration for <see cref="OnnxEmbedder"/>.
/// </summary>
public sealed class OnnxEmbedderOptions
{
    /// <summary>Path to the ONNX text embedding model (required).</summary>
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>Embedding model kind.</summary>
    public OnnxEmbeddingModelKind ModelKind { get; set; } = OnnxEmbeddingModelKind.EmbeddingGemma;

    /// <summary>Output embedding dimension (required, must be &gt; 0).</summary>
    public int Dimensions { get; set; }

    /// <summary>Optional CLIP vision ONNX path for <see cref="OnnxEmbedder.EmbedImageAsync"/>.</summary>
    public string? VisionModelPath { get; set; }
}
