namespace ZVec.Extensions.VectorData.Analyzers;

/// <summary>
/// Attribute and reflection member names used by ZVec AOT analyzers.
/// </summary>
internal static class AnalyzerMetadataNames
{
    internal const string VectorStoreRecord = "VectorStoreRecord";
    internal const string VectorStoreRecordAttribute = "VectorStoreRecordAttribute";
    internal const string ZVecId = "ZVecId";
    internal const string ZVecIdAttribute = "ZVecIdAttribute";
    internal const string VectorStoreKey = "VectorStoreKey";
    internal const string VectorStoreKeyAttribute = "VectorStoreKeyAttribute";

    internal const string GeneratedCode = "GeneratedCode";
    internal const string GeneratedCodeAttribute = "GeneratedCodeAttribute";
    internal const string GeneratorTypeName = "ZVecRecordMetadataGenerator";
    internal const string GeneratedMapperSuffix = "ZVecMetadataMapper";

    internal const string RequiresUnreferencedCodeAttribute = "RequiresUnreferencedCodeAttribute";
    internal const string RequiresUnreferencedCode = "RequiresUnreferencedCode";
    internal const string RequiresDynamicCodeAttribute = "RequiresDynamicCodeAttribute";
    internal const string RequiresDynamicCode = "RequiresDynamicCode";

    internal const string TypeClassName = "Type";
    internal const string ActivatorClassName = "Activator";
    internal const string AttributeClassName = "Attribute";

    internal static readonly string[] ReflectionMemberNames =
    [
        "GetProperties",
        "GetProperty",
        "GetField",
        "GetFields",
        "GetCustomAttribute",
        "GetCustomAttributes",
        "CreateInstance"
    ];
}
