using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZVec.Rag.ONNX;

namespace ZVec.Rag.ONNX.Tests;

public sealed class ClipImagePreprocessorTests
{
    [Fact]
    public void Preprocess_SolidRedPng_ProducesNchwTensorWithExpectedFirstChannel()
    {
        using var stream = CreateSolidPng(8, 8, 255, 0, 0);
        var preprocessor = new ClipImagePreprocessor();
        DenseTensor<float> tensor = preprocessor.Preprocess(stream, targetSize: 224);
        Assert.Equal(1 * 3 * 224 * 224, tensor.Length);
        float expected = (255f / 255f - OnnxConstants.ClipMeanR) / OnnxConstants.ClipStdR;
        Assert.Equal(expected, tensor[0, 0, 0, 0], precision: 5);
    }

    private static MemoryStream CreateSolidPng(int width, int height, byte r, byte g, byte b)
    {
        using var image = new Image<Rgb24>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgb24> row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    row[x] = new Rgb24(r, g, b);
                }
            }
        });

        var ms = new MemoryStream();
        image.SaveAsPng(ms);
        ms.Position = 0;
        return ms;
    }
}
