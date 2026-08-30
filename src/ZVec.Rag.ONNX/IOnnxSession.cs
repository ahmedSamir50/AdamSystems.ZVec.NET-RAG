using Microsoft.ML.OnnxRuntime.Tensors;

namespace ZVec.Rag.ONNX;

/// <summary>
/// Test seam and production abstraction over ONNX Runtime inference.
/// </summary>
internal interface IOnnxSession : IDisposable
{
    /// <summary>Runs inference on a flat input tensor.</summary>
    float[] Run(float[] input, int outputDimensions);
}
