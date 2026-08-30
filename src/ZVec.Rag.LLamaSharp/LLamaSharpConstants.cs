namespace ZVec.Rag.LLamaSharp;

/// <summary>
/// Centralized constants for the ZVec.Rag.LLamaSharp recipe package.
/// </summary>
public static class LLamaSharpConstants
{
    /// <summary>Default context size for GGUF model loading.</summary>
    public const int DefaultContextSize = 2048;

    /// <summary>Default GPU layer offload count (CPU-only).</summary>
    public const int DefaultGpuLayerCount = 0;

    /// <summary>Chat client metadata model id.</summary>
    public const string ChatClientModelId = "llamasharp-chat-client";

    /// <summary>Embedder metadata model id.</summary>
    public const string EmbedderModelId = "llamasharp-embedder";
}
