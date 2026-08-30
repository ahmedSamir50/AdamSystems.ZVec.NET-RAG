using System.Security.Cryptography;
using System.Text;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ZVec.Rag.ONNX;

/// <summary>
/// Production <see cref="IOnnxSession"/> backed by ONNX Runtime.
/// </summary>
internal sealed class OnnxRuntimeSession : IOnnxSession
{
    private readonly InferenceSession _session;
    private readonly int _outputDimensions;
    private bool _disposed;

    /// <summary>Initializes a new instance from a model file path.</summary>
    public OnnxRuntimeSession(string modelPath, int outputDimensions)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            throw new ArgumentException(OnnxErrorMessages.ModelPathRequired(), nameof(modelPath));
        }

        if (outputDimensions <= 0)
        {
            throw new ArgumentException(OnnxErrorMessages.InvalidDimensions(outputDimensions), nameof(outputDimensions));
        }

        _session = new InferenceSession(modelPath);
        _outputDimensions = outputDimensions;
    }

    /// <inheritdoc />
    public float[] Run(float[] input, int outputDimensions)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (outputDimensions != _outputDimensions)
        {
            throw new ArgumentException(OnnxErrorMessages.InvalidDimensions(outputDimensions), nameof(outputDimensions));
        }

        var inputTensor = new DenseTensor<float>(input, [1, input.Length]);
        string inputName = _session.InputMetadata.Keys.First();
        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(
            [NamedOnnxValue.CreateFromTensor(inputName, inputTensor)]);

        float[] output = results.First().AsEnumerable<float>().Take(outputDimensions).ToArray();
        if (output.Length != outputDimensions)
        {
            throw new InvalidOperationException(
                OnnxErrorMessages.OutputLengthMismatch(output.Length, outputDimensions));
        }

        return output;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session.Dispose();
    }
}
