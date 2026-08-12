namespace ZVec.Extensions.VectorData.Exceptions;

/// <summary>
/// Base exception thrown for all ZVec VectorData connector operational errors.
/// </summary>
public class ZVecVectorDataException : Exception
{
    /// <summary>Initializes a new instance of <see cref="ZVecVectorDataException"/> with a message.</summary>
    /// <param name="message">The error message.</param>
    public ZVecVectorDataException(string message) : base(message)
    {
    }

    /// <summary>Initializes a new instance of <see cref="ZVecVectorDataException"/> with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner cause of the failure.</param>
    public ZVecVectorDataException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
