namespace ZVec.Extensions.VectorData.Exceptions;

/// <summary>
/// Exception thrown when a LINQ expression or VectorDataFilter cannot be translated to a valid ZVec filter AST.
/// </summary>
public sealed class ZVecFilterTranslationException : ZVecVectorDataException
{
    /// <summary>Initializes a new instance of <see cref="ZVecFilterTranslationException"/> with a message.</summary>
    /// <param name="message">The translation error description.</param>
    public ZVecFilterTranslationException(string message) : base(message)
    {
    }

    /// <summary>Initializes a new instance of <see cref="ZVecFilterTranslationException"/> with a message and inner exception.</summary>
    /// <param name="message">The translation error description.</param>
    /// <param name="innerException">The inner cause of the translation failure.</param>
    public ZVecFilterTranslationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
