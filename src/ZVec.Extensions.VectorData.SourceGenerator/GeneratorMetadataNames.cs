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
    internal const string IndexKindArgument = "IndexKind";
    internal const string DistanceFunctionArgument = "DistanceFunction";

    internal const string ZVecFullTextSearchAttributeName = "ZVecFullTextSearchAttribute";
    internal const string ZVecFullTextSearchName = "ZVecFullTextSearch";

    internal const string GlobalSystemStringType = "global::System.String";
    internal const string StringTypeAlias = "string";
    internal const string GlobalSystemGuidType = "global::System.Guid";

    internal const string GeneratedMapperSuffix = "ZVecMetadataMapper";
    internal const string NullLiteral = "null";
    internal const string InvertIndexParamExpression = "new ZVecInvertIndexParam()";
    internal const string HnswIndexParamExpression = "new ZVecHnswIndexParam()";
    internal const string HnswIndexParamL2Expression = "ZVecVectorIndexResolver.CreateHnswIndexParam(metricType: ZVecMetricType.L2)";
    internal const string HnswIndexParamInnerProductExpression = "ZVecVectorIndexResolver.CreateHnswIndexParam(metricType: ZVecMetricType.Ip)";
    internal const string HnswIndexParamInt8Expression = "ZVecVectorIndexResolver.CreateHnswIndexParam(quantizeType: ZVecQuantizeType.Int8)";
    internal const string HnswIndexParamFp16Expression = "ZVecVectorIndexResolver.CreateHnswIndexParam(quantizeType: ZVecQuantizeType.Fp16)";
    internal const string HnswIndexParamUndefinedQuantizeExpression = "ZVecVectorIndexResolver.CreateHnswIndexParam(quantizeType: ZVecQuantizeType.Undefined)";
    internal const string FtsIndexParamExpression = "new ZVecFtsIndexParam()";
}
