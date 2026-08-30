namespace ZVec.Rag.ONNX;

/// <summary>
/// Centralized constants for the ZVec.Rag.ONNX recipe package.
/// </summary>
public static class OnnxConstants
{
    /// <summary>CLIP vector dimension for multimodal records.</summary>
    public const int ClipDimensions = 512;

    /// <summary>Source kind value for text chunks.</summary>
    public const string SourceKindText = "text";

    /// <summary>Source kind value for image chunks.</summary>
    public const string SourceKindImage = "image";

    /// <summary>CLIP ImageNet normalization mean (channel R).</summary>
    public const float ClipMeanR = 0.48145466f;

    /// <summary>CLIP ImageNet normalization mean (channel G).</summary>
    public const float ClipMeanG = 0.4578275f;

    /// <summary>CLIP ImageNet normalization mean (channel B).</summary>
    public const float ClipMeanB = 0.40821073f;

    /// <summary>CLIP ImageNet normalization std (channel R).</summary>
    public const float ClipStdR = 0.26862954f;

    /// <summary>CLIP ImageNet normalization std (channel G).</summary>
    public const float ClipStdG = 0.26130258f;

    /// <summary>CLIP ImageNet normalization std (channel B).</summary>
    public const float ClipStdB = 0.27577711f;

    /// <summary>Default CLIP input spatial size.</summary>
    public const int ClipDefaultImageSize = 224;

    /// <summary>Embedder metadata model id.</summary>
    public const string EmbedderModelId = "onnx-embedder";
}
