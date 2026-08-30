using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZVec.Rag.ONNX;
using ZVec.Rag.Options;
using ZVec.Rag.Schema;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency injection extensions for ZVec.Rag ONNX embedder recipe.
/// </summary>
public static class ZVecRagOnnxServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="OnnxEmbedder"/> as a singleton.
    /// When <see cref="ZVecRagOptions"/> is registered and dimensions are 768, sets <c>Embedder</c> when null.
    /// </summary>
    [RequiresUnreferencedCode("ONNX Runtime embedding is not trim-safe for Native AOT.")]
    public static IServiceCollection AddZVecRagOnnxEmbedder(
        this IServiceCollection services,
        Action<OnnxEmbedderOptions> configure)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var options = new OnnxEmbedderOptions();
        configure(options);

        if (string.IsNullOrWhiteSpace(options.ModelPath))
        {
            throw new ArgumentException(OnnxErrorMessages.ModelPathRequired(), nameof(configure));
        }

        if (options.Dimensions <= 0)
        {
            throw new ArgumentException(OnnxErrorMessages.InvalidDimensions(options.Dimensions), nameof(configure));
        }

        services.TryAddSingleton(options);
        services.TryAddSingleton<IOnnxSessionFactory, OnnxNativeSessionFactory>();
        services.TryAddSingleton(sp =>
        {
            OnnxEmbedderOptions opts = sp.GetRequiredService<OnnxEmbedderOptions>();
            IOnnxSessionFactory factory = sp.GetRequiredService<IOnnxSessionFactory>();
            IOnnxSession textSession = factory.CreateTextSession(opts);
            IOnnxSession? visionSession = factory.CreateVisionSession(opts);
            var embedder = new OnnxEmbedder(opts, textSession, visionSession);
            if (opts.Dimensions == ZVecRagRecordV1.DefaultDimensions
                && sp.GetService<ZVecRagOptions>() is { } ragOptions)
            {
                ragOptions.Embedder ??= embedder;
            }

            return embedder;
        });

        return services;
    }

    /// <summary>Creates ONNX sessions for DI and tests.</summary>
    internal interface IOnnxSessionFactory
    {
        /// <summary>Creates the text embedding session.</summary>
        IOnnxSession CreateTextSession(OnnxEmbedderOptions options);

        /// <summary>Creates the optional vision session.</summary>
        IOnnxSession? CreateVisionSession(OnnxEmbedderOptions options);
    }

    /// <summary>Default ONNX Runtime session factory.</summary>
    internal sealed class OnnxNativeSessionFactory : IOnnxSessionFactory
    {
        /// <inheritdoc />
        public IOnnxSession CreateTextSession(OnnxEmbedderOptions options)
            => new OnnxRuntimeSession(options.ModelPath, options.Dimensions);

        /// <inheritdoc />
        public IOnnxSession? CreateVisionSession(OnnxEmbedderOptions options)
        {
            if (options.ModelKind == OnnxEmbeddingModelKind.ClipText
                && !string.IsNullOrWhiteSpace(options.VisionModelPath))
            {
                return new OnnxRuntimeSession(options.VisionModelPath, OnnxConstants.ClipDimensions);
            }

            return null;
        }
    }
}
