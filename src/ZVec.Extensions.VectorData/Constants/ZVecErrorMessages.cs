namespace ZVec.Extensions.VectorData.Constants;

/// <summary>
/// Strongly-typed string constants and formatting helpers for error messages (Zero Magic Strings).
/// </summary>
public static class ZVecErrorMessages
{
    /// <summary>Error message when collection name is null, empty, or whitespace.</summary>
    public const string NullOrEmptyCollectionName = "Collection name cannot be null, empty, or whitespace.";

    /// <summary>Error message when the ZVec type model is unavailable for record mapping.</summary>
    public const string TypeModelUninitialized = "ZVec type model is unavailable. Add [VectorStore*] attributes and reference the source generator, or decorate the record with ZVec mapping attributes.";

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
    public static string UnsupportedStartsWithMethod(string fieldName = ZVecWellKnownMemberNames.UnknownMember) =>
        $"Field '{fieldName}': Filter method 'StartsWith' is not supported in LINQ filter expressions. " +
        "Remediation: Use ZVec full-text search (FTS) keyword queries for prefix matching, " +
        "or pre-compute a normalized field for exact equality filtering.";

    /// <summary>Error message when <c>string.EndsWith</c> is used in a LINQ filter expression.</summary>
    /// <param name="fieldName">Name of the filtered field, when available.</param>
    /// <returns>Formatted error message with remediation guidance.</returns>
    public static string UnsupportedEndsWithMethod(string fieldName = ZVecWellKnownMemberNames.UnknownMember) =>
        $"Field '{fieldName}': Filter method 'EndsWith' is not supported in LINQ filter expressions. " +
        "Remediation: Use ZVec full-text search (FTS) keyword queries for suffix matching, " +
        "or pre-compute a normalized field for exact equality filtering.";

    /// <summary>Error message when <c>Regex.IsMatch</c> is used in a LINQ filter expression.</summary>
    /// <param name="fieldName">Name of the filtered field, when available.</param>
    /// <returns>Formatted error message with remediation guidance.</returns>
    public static string UnsupportedRegexMethod(string fieldName = ZVecWellKnownMemberNames.UnknownMember) =>
        $"Field '{fieldName}': Filter method 'IsMatch' is not supported in LINQ filter expressions. " +
        "Remediation: Use ZVec full-text search (FTS) keyword queries for pattern matching, " +
        "or pre-compute a normalized field for exact equality filtering.";

    /// <summary>Error message when <c>string.Contains</c> is used in a LINQ filter expression.</summary>
    /// <param name="fieldName">Name of the filtered field, when available.</param>
    /// <returns>Formatted error message with remediation guidance.</returns>
    public static string UnsupportedStringContainsMethod(string fieldName = ZVecWellKnownMemberNames.UnknownMember) =>
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

    /// <summary>Error message when ContainAny receives a null search value.</summary>
    public const string ContainAnyRequiresNonNullValue = "ContainAny requires a non-null search value.";

    /// <summary>Error message when an IN clause collection is empty.</summary>
    public const string EmptyInClauseCollection = "Empty IN clause collection.";

    /// <summary>Error message when an IN clause collection is invalid.</summary>
    public const string InvalidInClauseCollection = "Invalid IN clause collection.";

    /// <summary>Formats error message for unsupported user-defined conversions between types.</summary>
    public static string UnsupportedUserDefinedConversion(string sourceTypeName, string targetTypeName) =>
        $"User-defined conversion from '{sourceTypeName}' to '{targetTypeName}' is not supported in filter expressions.";

    /// <summary>Formats error message for unsupported user-defined conversion operators.</summary>
    public static string UnsupportedUserDefinedConversionOperator(string declaringTypeName, string operatorName) =>
        $"User-defined conversion operator '{declaringTypeName}.{operatorName}' is not supported in filter expressions.";

    /// <summary>Formats error message when a method cannot be evaluated under AOT.</summary>
    public static string CannotEvaluateMethodUnderAot(string methodName, string reason) =>
        $"Cannot evaluate method '{methodName}' under AOT: {reason}";

    /// <summary>Formats error message when static expression evaluation fails under AOT.</summary>
    public static string CannotStaticallyEvaluateExpressionUnderAot(string expressionText) =>
        $"Cannot statically evaluate expression '{expressionText}' under AOT without dynamic compilation.";

    /// <summary>Formats error when embedder stamp manifest is missing on an existing collection.</summary>
    public static string ManifestMissing(string collectionPath) =>
        $"Embedder stamp manifest is missing for collection at '{collectionPath}'. " +
        "The native index exists but zvec_index_manifest.json was not found. " +
        "Remediation: delete the collection directory and re-ingest, or run IRagMigrationManager.";

    /// <summary>Formats error when embedder stamp manifest cannot be parsed.</summary>
    public static string ManifestCorrupt(string collectionPath) =>
        $"Embedder stamp manifest at '{collectionPath}' is corrupt or unreadable. " +
        "Remediation: delete the collection directory and re-ingest, or run IRagMigrationManager.";

    /// <summary>Formats error when manifest stamp fields do not match configured embedder/schema.</summary>
    public static string EmbedderStampMismatch(
        string collectionPath,
        string expectedModelId,
        string actualModelId,
        int expectedDimensions,
        int actualDimensions,
        string expectedQuantizeType,
        string actualQuantizeType,
        string expectedStorageDataType,
        string actualStorageDataType) =>
        $"Embedder stamp mismatch for collection at '{collectionPath}'. " +
        $"ModelId: expected '{expectedModelId}', actual '{actualModelId}'. " +
        $"Dimensions: expected {expectedDimensions}, actual {actualDimensions}. " +
        $"QuantizeType: expected '{expectedQuantizeType}', actual '{actualQuantizeType}'. " +
        $"StorageDataType: expected '{expectedStorageDataType}', actual '{actualStorageDataType}'. " +
        "Remediation: delete the collection storage, use a different StoragePath, or run IRagMigrationManager.";
}
