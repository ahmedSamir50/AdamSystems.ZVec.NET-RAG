using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.ML.OnnxRuntime.Tensors;
using ZVec.Rag.ONNX.Schema;
using ZVec.Rag.Schema;

namespace ZVec.Rag.ONNX;

/// <summary>
/// <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> adapter over ONNX Runtime models.
/// </summary>
[RequiresUnreferencedCode("ONNX Runtime embedding is not trim-safe for Native AOT.")]
public sealed class OnnxEmbedder : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly OnnxEmbedderOptions _options;
    private readonly IOnnxSession _textSession;
    private readonly IOnnxSession? _visionSession;
    private readonly ClipImagePreprocessor _imagePreprocessor = new();
    private readonly bool _ownsSessions;
    private bool _disposed;

    /// <summary>Initializes a new instance with production ONNX sessions.</summary>
    public OnnxEmbedder(OnnxEmbedderOptions options)
        : this(options, CreateTextSession(options), CreateVisionSession(options), ownsSessions: true)
    {
    }

    /// <summary>Initializes a new instance with injected sessions (for tests).</summary>
    internal OnnxEmbedder(
        OnnxEmbedderOptions options,
        IOnnxSession textSession,
        IOnnxSession? visionSession = null,
        bool ownsSessions = false)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(_options.ModelPath))
        {
            throw new ArgumentException(OnnxErrorMessages.ModelPathRequired(), nameof(options));
        }

        if (_options.Dimensions <= 0)
        {
            throw new ArgumentException(OnnxErrorMessages.InvalidDimensions(_options.Dimensions), nameof(options));
        }

        _textSession = textSession ?? throw new ArgumentNullException(nameof(textSession));
        _visionSession = visionSession;
        _ownsSessions = ownsSessions;
    }

    /// <inheritdoc />
    public EmbeddingGeneratorMetadata Metadata { get; } = new(OnnxConstants.EmbedderModelId);

    /// <inheritdoc />
    public TService? GetService<TService>(object? key = null) where TService : class => null;

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc />
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var embeddings = new List<Embedding<float>>();
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(OnnxErrorMessages.EmptyEmbedInput(), nameof(values));
            }

            float[] vector = _textSession.Run(CreateTextInput(value), _options.Dimensions);
            embeddings.Add(new Embedding<float>(vector));
        }

        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
    }

    /// <summary>
    /// Embeds an image stream when <see cref="OnnxEmbedderOptions.ModelKind"/> is <see cref="OnnxEmbeddingModelKind.ClipText"/>
    /// and <see cref="OnnxEmbedderOptions.VisionModelPath"/> is set.
    /// </summary>
    public async Task<Embedding<float>> EmbedImageAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (_options.ModelKind != OnnxEmbeddingModelKind.ClipText || string.IsNullOrWhiteSpace(_options.VisionModelPath))
        {
            throw new InvalidOperationException(OnnxErrorMessages.VisionModelRequired());
        }

        if (_visionSession is null)
        {
            throw new InvalidOperationException(OnnxErrorMessages.VisionModelRequired());
        }

        DenseTensor<float> tensor = _imagePreprocessor.Preprocess(imageStream);
        float[] flat = tensor.ToArray();
        float[] vector = _visionSession.Run(flat, OnnxConstants.ClipDimensions);
        await Task.CompletedTask.ConfigureAwait(false);
        return new Embedding<float>(vector);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsSessions)
        {
            _textSession.Dispose();
            _visionSession?.Dispose();
        }
    }

    private static IOnnxSession CreateTextSession(OnnxEmbedderOptions options)
        => new OnnxRuntimeSession(options.ModelPath, options.Dimensions);

    private static IOnnxSession? CreateVisionSession(OnnxEmbedderOptions options)
    {
        if (_optionsRequireVision(options))
        {
            return new OnnxRuntimeSession(options.VisionModelPath!, OnnxConstants.ClipDimensions);
        }

        return null;
    }

    private static bool _optionsRequireVision(OnnxEmbedderOptions options)
        => options.ModelKind == OnnxEmbeddingModelKind.ClipText
            && !string.IsNullOrWhiteSpace(options.VisionModelPath);

    private static float[] CreateTextInput(string text)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var input = new float[hash.Length];
        for (int i = 0; i < hash.Length; i++)
        {
            input[i] = hash[i] / 255f;
        }

        return input;
    }
}
