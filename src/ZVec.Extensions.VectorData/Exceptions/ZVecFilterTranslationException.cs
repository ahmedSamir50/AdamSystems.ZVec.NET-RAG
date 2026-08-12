using ZVec.Extensions.VectorData.Constants;

namespace ZVec.Extensions.VectorData.Exceptions;

/// <summary>
/// Exception thrown when a LINQ expression or VectorDataFilter cannot be translated to a valid ZVec filter AST.
/// </summary>
public sealed class ZVecFilterTranslationException : ZVecVectorDataException
{
    /// <summary>Initializes a new instance of <see cref="ZVecFilterTranslationException"/> with a message.</summary>
    /// <param name="message">The translation error description.</param>
    /// <param name="errorCode">Structured error code for programmatic handling.</param>
    public ZVecFilterTranslationException(string message, ZVecFilterErrorCode errorCode = ZVecFilterErrorCode.UnsupportedExpression) : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>Initializes a new instance of <see cref="ZVecFilterTranslationException"/> with a message and inner exception.</summary>
    /// <param name="message">The translation error description.</param>
    /// <param name="innerException">The inner cause of the translation failure.</param>
    /// <param name="errorCode">Structured error code for programmatic handling.</param>
    public ZVecFilterTranslationException(string message, Exception innerException, ZVecFilterErrorCode errorCode = ZVecFilterErrorCode.UnsupportedExpression) : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>Structured error code identifying the translation failure category.</summary>
    public ZVecFilterErrorCode ErrorCode { get; }
}
