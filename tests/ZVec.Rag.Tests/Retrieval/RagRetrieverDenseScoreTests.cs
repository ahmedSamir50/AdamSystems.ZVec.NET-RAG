using ZVec.Rag.Retrieval;

namespace ZVec.Rag.Tests.Retrieval;

public sealed class RagRetrieverDenseScoreTests
{
    [Fact]
    public void ComputeCosineSimilarity_IdenticalUnitVectors_ReturnsOne()
    {
        var vector = new float[] { 1f, 0f, 0f };
        float score = RagRetriever.ComputeCosineSimilarity(vector, vector);
        Assert.Equal(1f, score, precision: 3);
    }

    [Fact]
    public void ComputeCosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        float score = RagRetriever.ComputeCosineSimilarity(
            new float[] { 1f, 0f },
            new float[] { 0f, 1f });

        Assert.Equal(0f, score, precision: 3);
    }

    [Fact]
    public void ComputeCosineSimilarity_MismatchedDimensions_ReturnsZero()
    {
        float score = RagRetriever.ComputeCosineSimilarity(
            new float[] { 1f, 0f },
            new float[] { 1f, 0f, 0f });

        Assert.Equal(0f, score);
    }
}
