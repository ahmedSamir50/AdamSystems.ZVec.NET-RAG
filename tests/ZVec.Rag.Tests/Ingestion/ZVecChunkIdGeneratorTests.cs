using ZVec.Rag.Ingestion;

namespace ZVec.Rag.Tests.Ingestion;

/// <summary>
/// Unit tests for <see cref="ZVecChunkIdGenerator"/>.
/// </summary>
public sealed class ZVecChunkIdGeneratorTests
{
    [Fact]
    public void Compute_IsDeterministic_ForSameInputs()
    {
        string first = ZVecChunkIdGenerator.Compute("doc-uri", "whole-text-v1", 0);
        string second = ZVecChunkIdGenerator.Compute("doc-uri", "whole-text-v1", 0);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void Compute_ProducesDifferentIds_ForDifferentChunkIndexes()
    {
        string first = ZVecChunkIdGenerator.Compute("doc-uri", "whole-text-v1", 0);
        string second = ZVecChunkIdGenerator.Compute("doc-uri", "whole-text-v1", 1);

        Assert.NotEqual(first, second);
    }
}
