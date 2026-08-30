using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using ZVec.Rag.ONNX;

namespace ZVec.Rag.ONNX.Tests;

internal sealed class FakeOnnxSession : IOnnxSession
{
    private readonly int _dimensions;
    private bool _disposed;

    public FakeOnnxSession(int dimensions) => _dimensions = dimensions;

    public float[] Run(float[] input, int outputDimensions)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (outputDimensions != _dimensions)
        {
            throw new ArgumentException(OnnxErrorMessages.InvalidDimensions(outputDimensions), nameof(outputDimensions));
        }

        var vector = new float[_dimensions];
        byte[] hash = SHA256.HashData(MemoryMarshal.AsBytes(input.AsSpan()));
        for (int i = 0; i < _dimensions; i++)
        {
            vector[i] = (hash[i % hash.Length] / 255f) - 0.5f;
        }

        float magnitude = 0f;
        for (int i = 0; i < vector.Length; i++)
        {
            magnitude += vector[i] * vector[i];
        }

        magnitude = MathF.Sqrt(magnitude);
        if (magnitude > 0f)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= magnitude;
            }
        }

        return vector;
    }

    public void Dispose() => _disposed = true;
}
