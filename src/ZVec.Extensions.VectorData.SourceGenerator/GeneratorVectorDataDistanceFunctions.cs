using Microsoft.Extensions.VectorData;

namespace ZVec.Extensions.VectorData.SourceGenerator;

/// <summary>
/// Canonical distance-function tokens from <c>Microsoft.Extensions.VectorData.DistanceFunction</c> for the source generator.
/// </summary>
internal static class GeneratorVectorDataDistanceFunctions
{
    internal static readonly string EuclideanDistance = DistanceFunction.EuclideanDistance;
    internal static readonly string EuclideanSquaredDistance = DistanceFunction.EuclideanSquaredDistance;
    internal static readonly string DotProductSimilarity = DistanceFunction.DotProductSimilarity;
    internal static readonly string NegativeDotProductSimilarity = DistanceFunction.NegativeDotProductSimilarity;
    internal static readonly string CosineDistance = DistanceFunction.CosineDistance;
    internal static readonly string CosineSimilarity = DistanceFunction.CosineSimilarity;
}
