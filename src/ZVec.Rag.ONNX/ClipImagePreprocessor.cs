using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ZVec.Rag.ONNX;

/// <summary>
/// CLIP image preprocessing (NCHW tensor) using SixLabors.ImageSharp.
/// </summary>
public sealed class ClipImagePreprocessor
{
    /// <summary>
    /// Preprocesses an image stream into a CLIP NCHW tensor [1, 3, H, W].
    /// </summary>
    public DenseTensor<float> Preprocess(Stream imageStream, int targetSize = OnnxConstants.ClipDefaultImageSize)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        using Image<Rgb24> image = Image.Load<Rgb24>(imageStream);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(targetSize, targetSize),
            Mode = ResizeMode.Crop
        }));

        var tensor = new DenseTensor<float>([1, 3, targetSize, targetSize]);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<Rgb24> pixelRow = accessor.GetRowSpan(y);
                for (int x = 0; x < accessor.Width; x++)
                {
                    tensor[0, 0, y, x] = (pixelRow[x].R / 255.0f - OnnxConstants.ClipMeanR) / OnnxConstants.ClipStdR;
                    tensor[0, 1, y, x] = (pixelRow[x].G / 255.0f - OnnxConstants.ClipMeanG) / OnnxConstants.ClipStdG;
                    tensor[0, 2, y, x] = (pixelRow[x].B / 255.0f - OnnxConstants.ClipMeanB) / OnnxConstants.ClipStdB;
                }
            }
        });

        return tensor;
    }
}
