using Microsoft.Extensions.AI;
using ZVec.Rag.LLamaSharp;
using ZVec.Rag.Schema;

namespace ZVec.Rag.LLamaSharp.Tests;

public sealed class LLamaSharpEmbedderTests
{
    [Fact]
    public async Task GenerateAsync_Returns768DimensionalVector()
    {
        using var embedder = new LLamaSharpEmbedder(new FakeLlamaSharpSession([], dimensions: ZVecRagRecordV1.DefaultDimensions));
        GeneratedEmbeddings<Embedding<float>> result = await embedder.GenerateAsync(
            ["hello"],
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(result);
        Assert.Equal(ZVecRagRecordV1.DefaultDimensions, result[0].Vector.Length);
    }

    [Fact]
    public async Task GenerateAsync_EmptyInput_ThrowsArgumentException()
    {
        using var embedder = new LLamaSharpEmbedder(new FakeLlamaSharpSession([]));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            embedder.GenerateAsync([""], cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Constructor_NullSession_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new LLamaSharpEmbedder(null!));
    }
}
