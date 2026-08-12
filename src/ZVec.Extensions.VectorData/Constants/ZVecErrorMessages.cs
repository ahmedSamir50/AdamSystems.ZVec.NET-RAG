namespace ZVec.Extensions.VectorData.Constants;

/// <summary>
/// Strongly-typed string constants and formatting helpers for error messages (Zero Magic Strings).
/// </summary>
public static class ZVecErrorMessages
{
    /// <summary>Error message when collection name is null, empty, or whitespace.</summary>
    public const string NullOrEmptyCollectionName = "Collection name cannot be null, empty, or whitespace.";

    /// <summary>Error message when expression text is null, empty, or whitespace.</summary>
    public const string NullOrEmptyExpressionText = "Expression text cannot be null, empty, or whitespace.";

    /// <summary>Formats error message when record type is a value type struct instead of a reference class.</summary>
    /// <param name="typeName">Name of the invalid record type.</param>
    /// <returns>Formatted error message.</returns>
    public static string RecordMustBeClass(string typeName) =>
        $"Record type '{typeName}' must be a reference class.";

    /// <summary>Formats error message when input vector type is not ReadOnlyMemory of float.</summary>
    /// <param name="typeName">Name of the unsupported input vector type.</param>
    /// <returns>Formatted error message.</returns>
    public static string UnsupportedVectorType(string typeName) =>
        $"Search vector type '{typeName}' is not supported. Query vectors must be ReadOnlyMemory<float>.";

    /// <summary>Formats error message when a requested collection is not found.</summary>
    /// <param name="collectionName">Name of the missing collection.</param>
    /// <returns>Formatted error message.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="collectionName"/> is null, empty, or whitespace.</exception>
    public static string CollectionNotFound(string collectionName)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
            throw new ArgumentException(NullOrEmptyCollectionName, nameof(collectionName));

        return $"Collection '{collectionName}' does not exist.";
    }

    /// <summary>Formats error message when a LINQ expression cannot be translated to a ZVec filter.</summary>
    /// <param name="expressionText">String representation of the unsupported expression.</param>
    /// <returns>Formatted error message.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="expressionText"/> is null, empty, or whitespace.</exception>
    public static string UnsupportedFilterExpression(string expressionText)
    {
        if (string.IsNullOrWhiteSpace(expressionText))
            throw new ArgumentException(NullOrEmptyExpressionText, nameof(expressionText));

        return $"Expression '{expressionText}' cannot be translated to ZVecFilterBuilder.";
    }

    /// <summary>Error message when <c>string.StartsWith</c> is used in a LINQ filter expression.</summary>
    /// <param name="fieldName">Name of the filtered field, when available.</param>
    /// <returns>Formatted error message with remediation guidance.</returns>
    public static string UnsupportedStartsWithMethod(string fieldName = "unknown") =>
        $"Field '{fieldName}': Filter method 'StartsWith' is not supported in LINQ filter expressions. " +
        "Remediation: Use ZVec full-text search (FTS) keyword queries for prefix matching, " +
        "or pre-compute a normalized field for exact equality filtering.";

    /// <summary>Error message when <c>string.EndsWith</c> is used in a LINQ filter expression.</summary>
    /// <param name="fieldName">Name of the filtered field, when available.</param>
    /// <returns>Formatted error message with remediation guidance.</returns>
    public static string UnsupportedEndsWithMethod(string fieldName = "unknown") =>
        $"Field '{fieldName}': Filter method 'EndsWith' is not supported in LINQ filter expressions. " +
        "Remediation: Use ZVec full-text search (FTS) keyword queries for suffix matching, " +
        "or pre-compute a normalized field for exact equality filtering.";

    /// <summary>Error message when <c>Regex.IsMatch</c> is used in a LINQ filter expression.</summary>
    /// <param name="fieldName">Name of the filtered field, when available.</param>
    /// <returns>Formatted error message with remediation guidance.</returns>
    public static string UnsupportedRegexMethod(string fieldName = "unknown") =>
        $"Field '{fieldName}': Filter method 'IsMatch' is not supported in LINQ filter expressions. " +
        "Remediation: Use ZVec full-text search (FTS) keyword queries for pattern matching, " +
        "or pre-compute a normalized field for exact equality filtering.";

    /// <summary>Error message when <c>string.Contains</c> is used in a LINQ filter expression.</summary>
    /// <param name="fieldName">Name of the filtered field, when available.</param>
    /// <returns>Formatted error message with remediation guidance.</returns>
    public static string UnsupportedStringContainsMethod(string fieldName = "unknown") =>
        $"Field '{fieldName}': string.Contains is not supported in LINQ filters. " +
        "Remediation: Use ZVec full-text search (FTS) keyword search, or ContainAny on collection properties.";

    /// <summary>Error message when ContainAny is applied to a nested member access (e.g. x.Order.Tags.Contains).</summary>
    /// <param name="expressionText">String representation of the nested member expression.</param>
    /// <returns>Formatted error message with remediation guidance.</returns>
    public static string UnsupportedNestedMemberAccess(string expressionText)
    {
        if (string.IsNullOrWhiteSpace(expressionText))
            throw new ArgumentException(NullOrEmptyExpressionText, nameof(expressionText));

        return $"ContainAny on nested member access ('{expressionText}') is not supported. " +
               "Only direct record properties are allowed. Remediation: flatten the nested collection " +
               "into a top-level property on the record, or pre-project the nested values before filtering.";
    }
}
