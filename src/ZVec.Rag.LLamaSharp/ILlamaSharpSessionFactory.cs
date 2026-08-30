namespace ZVec.Rag.LLamaSharp;

/// <summary>
/// Creates <see cref="ILlamaSharpSession"/> instances for DI and tests.
/// </summary>
internal interface ILlamaSharpSessionFactory
{
    /// <summary>Creates a session for the given options.</summary>
    ILlamaSharpSession Create(LLamaSharpOptions options);
}

/// <summary>
/// Default factory that loads native GGUF weights via LLamaSharp.
/// </summary>
internal sealed class LlamaSharpNativeSessionFactory : ILlamaSharpSessionFactory
{
    /// <inheritdoc />
    public ILlamaSharpSession Create(LLamaSharpOptions options) => new LlamaSharpNativeSession(options);
}
