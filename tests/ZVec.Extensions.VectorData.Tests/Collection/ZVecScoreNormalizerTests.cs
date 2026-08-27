using ZVec.Extensions.VectorData.Collection;
using ZVec.NET;
using Xunit;

namespace ZVec.Extensions.VectorData.Tests;

/// <summary>
/// Unit tests for <see cref="ZVecScoreNormalizer"/> formula matrix (Cosine, L2, Ip).
/// </summary>
public sealed class ZVecScoreNormalizerTests
{
    [Fact]
    public void ToSimilarity_CosineDistance_ProducesOneMinusDistance()
    {
        Assert.Equal(0.9f, ZVecScoreNormalizer.ToSimilarity(0.1f, ZVecMetricType.Cosine));
        Assert.Equal(0.5f, ZVecScoreNormalizer.ToSimilarity(0.5f, ZVecMetricType.Cosine));
    }

    [Fact]
    public void ToSimilarity_L2Distance_ProducesInverseFormula()
    {
        Assert.Equal(1.0f, ZVecScoreNormalizer.ToSimilarity(0.0f, ZVecMetricType.L2));
        Assert.Equal(0.5f, ZVecScoreNormalizer.ToSimilarity(1.0f, ZVecMetricType.L2));
        Assert.Equal(0.33333334f, ZVecScoreNormalizer.ToSimilarity(2.0f, ZVecMetricType.L2));
    }

    [Fact]
    public void ToSimilarity_InnerProduct_PassthroughRawScore()
    {
        Assert.Equal(0.42f, ZVecScoreNormalizer.ToSimilarity(0.42f, ZVecMetricType.Ip));
        Assert.Equal(-0.1f, ZVecScoreNormalizer.ToSimilarity(-0.1f, ZVecMetricType.Ip));
    }

    [Fact]
    public void ToSimilarity_Cosine_HigherDistance_ProducesLowerSimilarity()
    {
        float near = ZVecScoreNormalizer.ToSimilarity(0.1f, ZVecMetricType.Cosine);
        float far = ZVecScoreNormalizer.ToSimilarity(0.9f, ZVecMetricType.Cosine);
        Assert.True(near > far, $"Near similarity ({near}) must exceed far similarity ({far}).");
    }

    [Fact]
    public void ToSimilarity_L2_HigherDistance_ProducesLowerSimilarity()
    {
        float near = ZVecScoreNormalizer.ToSimilarity(0.1f, ZVecMetricType.L2);
        float far = ZVecScoreNormalizer.ToSimilarity(5.0f, ZVecMetricType.L2);
        Assert.True(near > far, $"Near L2 similarity ({near}) must exceed far similarity ({far}).");
    }

    [Fact]
    public void ToSimilarity_UnknownMetric_FallsBackToCosineFormula()
    {
        Assert.Equal(0.8f, ZVecScoreNormalizer.ToSimilarity(0.2f, default));
    }
}
