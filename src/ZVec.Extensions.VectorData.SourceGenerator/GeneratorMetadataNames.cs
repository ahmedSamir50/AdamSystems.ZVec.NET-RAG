namespace ZVec.Extensions.VectorData.SourceGenerator;

/// <summary>
/// Attribute, named-argument, and emission tokens for <see cref="ZVecRecordMetadataGenerator"/>.
/// </summary>
internal static class GeneratorMetadataNames
{
    internal const string VectorStoreAttributeToken = "VectorStore";
    internal const string VectorStoreKeyToken = "VectorStoreKey";
    internal const string VectorStoreVectorToken = "VectorStoreVector";
    internal const string VectorStoreDataToken = "VectorStoreData";

    internal const string StorageNameArgument = "StorageName";
    internal const string IsFullTextIndexedArgument = "IsFullTextIndexed";
    internal const string IsIndexedArgument = "IsIndexed";

    internal const string ZVecFullTextSearchAttributeName = "ZVecFullTextSearchAttribute";
    internal const string ZVecFullTextSearchName = "ZVecFullTextSearch";

    internal const string GlobalSystemStringType = "global::System.String";
    internal const string StringTypeAlias = "string";
    internal const string GlobalSystemGuidType = "global::System.Guid";

    internal const string GeneratedMapperSuffix = "ZVecMetadataMapper";
    internal const string NullLiteral = "null";
    internal const string InvertIndexParamExpression = "new ZVecInvertIndexParam()";
    internal const string HnswIndexParamExpression = "new ZVecHnswIndexParam()";
    internal const string FtsIndexParamExpression = "new ZVecFtsIndexParam()";
}
