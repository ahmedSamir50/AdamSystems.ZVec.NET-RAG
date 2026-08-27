using Microsoft.Extensions.DependencyInjection.Extensions;
using ZVec.Extensions.VectorData.Store;
using ZVec.Rag;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Generation;
using ZVec.Rag.Ingestion;
using ZVec.Rag.Internal;
using ZVec.Rag.Options;
using ZVec.Rag.Retrieval;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency injection extensions for registering the ZVec.Rag pipeline.
/// </summary>
public static class ZVecRagServiceCollectionExtensions
{
    /// <summary>
    /// Adds ZVec.Rag services and idempotently registers <c>AddZVecVectorStore</c>.
    /// </summary>
    public static IServiceCollection AddZVecRag(
        this IServiceCollection services,
        Action<ZVecRagOptions>? configure = null)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        var ragOptions = new ZVecRagOptions();
        configure?.Invoke(ragOptions);

        if (string.IsNullOrWhiteSpace(ragOptions.VectorStore.StoragePath))
        {
            ragOptions.VectorStore.StoragePath = ragOptions.StoragePath;
        }

        if (string.IsNullOrWhiteSpace(ragOptions.VectorStore.ModelId))
        {
            ragOptions.VectorStore.ModelId = "zvec-rag-default";
        }

        services.TryAddSingleton(ragOptions);

        services.AddZVecVectorStore(opts =>
        {
            opts.StoragePath = ragOptions.VectorStore.StoragePath;
            opts.MaxConcurrentNativeCalls = ragOptions.VectorStore.MaxConcurrentNativeCalls;
            opts.EnableMmap = ragOptions.VectorStore.EnableMmap;
            opts.ReadOnly = ragOptions.VectorStore.ReadOnly;
            opts.MemoryLimitMb = ragOptions.VectorStore.MemoryLimitMb;
            opts.ModelId = ragOptions.VectorStore.ModelId;
            opts.DefaultQuantizeType = ragOptions.VectorStore.DefaultQuantizeType;
            opts.Factory = ragOptions.VectorStore.Factory;
        });

        services.TryAddScoped<RagCollectionProvider>();
        services.TryAddScoped<ContextPacker>();
        services.TryAddScoped<IRagIngestor, RagIngestor>();
        services.TryAddScoped<IRagRetriever, RagRetriever>();
        services.TryAddScoped<IRagGenerator, RagGenerator>();
        services.TryAddScoped<IRagPipeline, RagPipeline>();

        return services;
    }
}
