using ZVec.NET;

namespace ZVec.Extensions.VectorData.Collection;

/// <summary>
/// Converts native ZVec distance / raw scores into Microsoft.Extensions.VectorData similarity scores
/// where higher values indicate better matches.
/// </summary>
/// <remarks>
/// Dense vector queries return native distances (lower = closer). Hybrid RRF fusion returns fused
/// rank scores (already higher = better) and must not pass through this normalizer.
/// </remarks>
public static class ZVecScoreNormalizer
{
    /// <summary>
    /// Normalizes a native dense-query score using the configured metric type.
    /// </summary>
    /// <param name="nativeScore">Raw score from the native query engine (distance or inner product).</param>
    /// <param name="metricType">Index metric configured on the dense vector.</param>
    /// <returns>Similarity score where higher indicates a better match.</returns>
    public static float ToSimilarity(float nativeScore, ZVecMetricType metricType)
    {
        return metricType switch
        {
            ZVecMetricType.Cosine => 1.0f - nativeScore,
            ZVecMetricType.L2 => 1.0f / (1.0f + nativeScore),
            ZVecMetricType.Ip => nativeScore,
            _ => 1.0f - nativeScore
        };
    }
}
