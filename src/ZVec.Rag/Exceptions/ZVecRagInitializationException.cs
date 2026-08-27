namespace ZVec.Rag.Exceptions;

/// <summary>
/// Thrown when the RAG pipeline cannot initialize due to embedder stamp or storage mismatch.
/// </summary>
public sealed class ZVecRagInitializationException : Exception
{
    /// <summary>Initializes a new instance with remediation guidance.</summary>
    public ZVecRagInitializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance with remediation guidance.</summary>
    public ZVecRagInitializationException(string message)
        : base(message)
    {
    }
}
