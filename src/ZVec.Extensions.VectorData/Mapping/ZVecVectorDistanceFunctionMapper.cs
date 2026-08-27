namespace ZVec.Extensions.VectorData.Mapping;

using ZVec.Extensions.VectorData.Constants;
using ZVec.NET;

/// <summary>
/// Maps <see cref="Microsoft.Extensions.VectorData.VectorStoreVectorProperty.DistanceFunction"/> values to native <see cref="ZVecMetricType"/>.
/// </summary>
public static class ZVecVectorDistanceFunctionMapper
{
    /// <summary>
    /// Maps a VectorData distance function to the native ZVec dense metric type.
    /// </summary>
    public static ZVecMetricType ToMetricType(Microsoft.Extensions.VectorData.VectorStoreVectorProperty vectorProperty)
    {
        if (!string.IsNullOrWhiteSpace(vectorProperty.DistanceFunction))
        {
            return MapDistanceFunction(vectorProperty.DistanceFunction);
        }

        if (!string.IsNullOrWhiteSpace(vectorProperty.IndexKind))
        {
            if (string.Equals(vectorProperty.IndexKind, nameof(ZVecMetricType.L2), StringComparison.OrdinalIgnoreCase))
            {
                return ZVecMetricType.L2;
            }

            if (string.Equals(vectorProperty.IndexKind, nameof(ZVecMetricType.Ip), StringComparison.OrdinalIgnoreCase))
            {
                return ZVecMetricType.Ip;
            }

            if (string.Equals(vectorProperty.IndexKind, nameof(ZVecMetricType.Cosine), StringComparison.OrdinalIgnoreCase))
            {
                return ZVecMetricType.Cosine;
            }
        }

        return ZVecMetricType.Cosine;
    }

    private static ZVecMetricType MapDistanceFunction(string distanceFunction)
    {
        if (string.Equals(distanceFunction, ZVecVectorDataDistanceFunctions.EuclideanDistance, StringComparison.Ordinal) ||
            string.Equals(distanceFunction, ZVecVectorDataDistanceFunctions.EuclideanSquaredDistance, StringComparison.Ordinal))
        {
            return ZVecMetricType.L2;
        }

        if (string.Equals(distanceFunction, ZVecVectorDataDistanceFunctions.DotProductSimilarity, StringComparison.Ordinal) ||
            string.Equals(distanceFunction, ZVecVectorDataDistanceFunctions.NegativeDotProductSimilarity, StringComparison.Ordinal))
        {
            return ZVecMetricType.Ip;
        }

        if (string.Equals(distanceFunction, ZVecVectorDataDistanceFunctions.CosineDistance, StringComparison.Ordinal) ||
            string.Equals(distanceFunction, ZVecVectorDataDistanceFunctions.CosineSimilarity, StringComparison.Ordinal))
        {
            return ZVecMetricType.Cosine;
        }

        return ZVecMetricType.Cosine;
    }
}
