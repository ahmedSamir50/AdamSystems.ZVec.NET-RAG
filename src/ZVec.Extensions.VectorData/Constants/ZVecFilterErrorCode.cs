namespace ZVec.Extensions.VectorData.Constants;

/// <summary>
/// Structured error codes for filter translation failures.
/// </summary>
public enum ZVecFilterErrorCode
{
    /// <summary>Generic unsupported filter expression shape.</summary>
    UnsupportedExpression = 0,

    /// <summary><c>string.StartsWith</c> is not supported in LINQ filters.</summary>
    UnsupportedStartsWith = 1,

    /// <summary><c>string.EndsWith</c> is not supported in LINQ filters.</summary>
    UnsupportedEndsWith = 2,

    /// <summary><c>Regex.IsMatch</c> is not supported in LINQ filters.</summary>
    UnsupportedRegex = 3,

    /// <summary><c>string.Contains</c> is not supported in LINQ filters.</summary>
    UnsupportedStringContains = 4,

    /// <summary>User-defined conversion operator is not supported in filter expressions.</summary>
    UnsupportedUserDefinedConversion = 5
}
