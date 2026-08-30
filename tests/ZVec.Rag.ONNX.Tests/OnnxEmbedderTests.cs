using Microsoft.Extensions.AI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZVec.Rag.ONNX;
using ZVec.Rag.Schema;

namespace ZVec.Rag.ONNX.Tests;

public sealed class OnnxEmbedderTests
{
    [Fact]
    public async Task GenerateAsync_OneString_ReturnsEmbeddingOfRequestedDimension()
    {
        var options = new OnnxEmbedderOptions
        {
            ModelPath = "fake.onnx",
            ModelKind = OnnxEmbeddingModelKind.EmbeddingGemma,
            Dimensions = ZVecRagRecordV1.DefaultDimensions
        };
        using var embedder = new OnnxEmbedder(options, new FakeOnnxSession(options.Dimensions));
        GeneratedEmbeddings<Embedding<float>> result = await embedder.GenerateAsync(
            ["hello"],
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(result);
        Assert.Equal(ZVecRagRecordV1.DefaultDimensions, result[0].Vector.Length);
    }

    [Fact]
    public async Task GenerateAsync_BatchOfTwo_ReturnsTwoEmbeddings()
    {
        var options = new OnnxEmbedderOptions { ModelPath = "fake.onnx", Dimensions = 384, ModelKind = OnnxEmbeddingModelKind.MiniLm };
        using var embedder = new OnnxEmbedder(options, new FakeOnnxSession(384));
        GeneratedEmbeddings<Embedding<float>> result = await embedder.GenerateAsync(
            ["a", "b"],
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GenerateAsync_EmptyInput_ThrowsArgumentException()
    {
        var options = new OnnxEmbedderOptions { ModelPath = "fake.onnx", Dimensions = 768 };
        using var embedder = new OnnxEmbedder(options, new FakeOnnxSession(768));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            embedder.GenerateAsync([""], cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Constructor_MissingModelPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new OnnxEmbedder(new OnnxEmbedderOptions { Dimensions = 768 }));
    }

    [Fact]
    public async Task EmbedImageAsync_WithoutVisionPath_ThrowsInvalidOperationException()
    {
        var options = new OnnxEmbedderOptions
        {
            ModelPath = "fake.onnx",
            ModelKind = OnnxEmbeddingModelKind.ClipText,
            Dimensions = OnnxConstants.ClipDimensions
        };
        using var embedder = new OnnxEmbedder(options, new FakeOnnxSession(OnnxConstants.ClipDimensions));
        using var stream = CreateSolidPng(8, 8);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            embedder.EmbedImageAsync(stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EmbedImageAsync_WithVisionSession_Returns512DimensionalVector()
    {
        var options = new OnnxEmbedderOptions
        {
            ModelPath = "text.onnx",
            VisionModelPath = "vision.onnx",
            ModelKind = OnnxEmbeddingModelKind.ClipText,
            Dimensions = OnnxConstants.ClipDimensions
        };
        using var embedder = new OnnxEmbedder(
            options,
            new FakeOnnxSession(OnnxConstants.ClipDimensions),
            new FakeOnnxSession(OnnxConstants.ClipDimensions));
        using var stream = CreateSolidPng(8, 8);
        Embedding<float> result = await embedder.EmbedImageAsync(stream, TestContext.Current.CancellationToken);
        Assert.Equal(OnnxConstants.ClipDimensions, result.Vector.Length);
    }

    private static MemoryStream CreateSolidPng(int width, int height)
    {
        using var image = new Image<Rgb24>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgb24> row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    row[x] = new Rgb24(255, 0, 0);
                }
            }
        });

        var ms = new MemoryStream();
        image.SaveAsPng(ms);
        ms.Position = 0;
        return ms;
    }
}
