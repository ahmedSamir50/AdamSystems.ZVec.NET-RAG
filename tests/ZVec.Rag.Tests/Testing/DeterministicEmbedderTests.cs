using Microsoft.Extensions.AI;
using ZVec.Rag.Testing;

namespace ZVec.Rag.Tests.Testing;

/// <summary>
/// Unit tests for <see cref="DeterministicEmbedder"/>.
/// </summary>
public sealed class DeterministicEmbedderTests
{
    [Fact]
    public void CreateVector_ReturnsUnitLengthVector_WithDefault768Dimensions()
    {
        var embedder = new DeterministicEmbedder();
        ReadOnlyMemory<float> vector = embedder.CreateVector("hello world");

        Assert.Equal(768, vector.Length);

        float magnitude = 0f;
        foreach (float value in vector.Span)
        {
            magnitude += value * value;
        }

        Assert.InRange(MathF.Sqrt(magnitude), 0.999f, 1.001f);
    }

    [Fact]
    public void CreateVector_IsDeterministic_ForSameInput()
    {
        var embedder = new DeterministicEmbedder();
        ReadOnlyMemory<float> first = embedder.CreateVector("repeatable");
        ReadOnlyMemory<float> second = embedder.CreateVector("repeatable");

        Assert.Equal(first.ToArray(), second.ToArray());
    }

    [Fact]
    public void CreateVector_ProducesDifferentVectors_ForDifferentInputs()
    {
        var embedder = new DeterministicEmbedder();
        ReadOnlyMemory<float> first = embedder.CreateVector("alpha");
        ReadOnlyMemory<float> second = embedder.CreateVector("beta");

        Assert.NotEqual(first.ToArray(), second.ToArray());
    }

    [Fact]
    public async Task GenerateAsync_ReturnsEmbeddingPerInput()
    {
        var embedder = new DeterministicEmbedder(4);
        GeneratedEmbeddings<Embedding<float>> result = await embedder.GenerateAsync(
            ["a", "b"],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Equal(4, result[0].Vector.Length);
        Assert.Equal(4, result[1].Vector.Length);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsOperationCanceledException_WhenTokenCanceled()
    {
        var embedder = new DeterministicEmbedder();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await embedder.GenerateAsync(["x"], cancellationToken: cts.Token));
    }

    [Fact]
    public void Constructor_ThrowsArgumentOutOfRangeException_WhenDimensionsNonPositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicEmbedder(0));
    }
}
