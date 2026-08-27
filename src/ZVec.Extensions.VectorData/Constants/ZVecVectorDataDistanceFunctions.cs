namespace ZVec.Extensions.VectorData.Constants;

/// <summary>
/// Canonical distance-function tokens from <c>Microsoft.Extensions.VectorData.DistanceFunction</c> (zero magic strings).
/// </summary>
public static class ZVecVectorDataDistanceFunctions
{
    /// <summary>Cosine similarity distance function token.</summary>
    public static readonly string CosineSimilarity = Microsoft.Extensions.VectorData.DistanceFunction.CosineSimilarity;

    /// <summary>Cosine distance function token.</summary>
    public static readonly string CosineDistance = Microsoft.Extensions.VectorData.DistanceFunction.CosineDistance;

    /// <summary>Euclidean (L2) distance function token.</summary>
    public static readonly string EuclideanDistance = Microsoft.Extensions.VectorData.DistanceFunction.EuclideanDistance;

    /// <summary>Squared Euclidean distance function token.</summary>
    public static readonly string EuclideanSquaredDistance = Microsoft.Extensions.VectorData.DistanceFunction.EuclideanSquaredDistance;

    /// <summary>Dot-product similarity distance function token.</summary>
    public static readonly string DotProductSimilarity = Microsoft.Extensions.VectorData.DistanceFunction.DotProductSimilarity;

    /// <summary>Negative dot-product similarity distance function token.</summary>
    public static readonly string NegativeDotProductSimilarity = Microsoft.Extensions.VectorData.DistanceFunction.NegativeDotProductSimilarity;
}
