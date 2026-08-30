namespace ZVec.Rag.LLamaSharp;

/// <summary>
/// Configuration for LLamaSharp GGUF model loading.
/// </summary>
public sealed class LLamaSharpOptions
{
    /// <summary>Path to the GGUF model file (required).</summary>
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>Maximum context tokens for the model.</summary>
    public int ContextSize { get; set; } = LLamaSharpConstants.DefaultContextSize;

    /// <summary>Number of layers to offload to GPU (0 = CPU only).</summary>
    public int GpuLayerCount { get; set; } = LLamaSharpConstants.DefaultGpuLayerCount;
}
