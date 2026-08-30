namespace ZVec.Rag.ONNX;

/// <summary>
/// Strongly-typed error message templates for ZVec.Rag.ONNX.
/// </summary>
public static class OnnxErrorMessages
{
    /// <summary>Formats error when model path is missing.</summary>
    public static string ModelPathRequired() =>
        "OnnxEmbedderOptions.ModelPath is required. Set ZVEC_ONNX_MODEL or provide an ONNX file path.";

    /// <summary>Formats error when dimensions are invalid.</summary>
    public static string InvalidDimensions(int dimensions) =>
        $"OnnxEmbedderOptions.Dimensions must be positive. Got {dimensions}.";

    /// <summary>Formats error when vision model is required for image embed.</summary>
    public static string VisionModelRequired() =>
        "EmbedImageAsync requires ModelKind ClipText and VisionModelPath to be set.";

    /// <summary>Formats error when embed input is empty.</summary>
    public static string EmptyEmbedInput() =>
        "Embed input cannot be null or empty.";

    /// <summary>Formats error when ONNX output length does not match expected dimensions.</summary>
    public static string OutputLengthMismatch(int actual, int expected) =>
        $"ONNX output length {actual} != expected {expected}.";
}
