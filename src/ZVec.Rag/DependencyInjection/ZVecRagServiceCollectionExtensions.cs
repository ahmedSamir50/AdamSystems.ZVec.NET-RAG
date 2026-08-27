using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.ML.Tokenizers;
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

        services.TryAddSingleton<ZVecTokenizerResolver>();
        services.TryAddSingleton<IRagDocumentReader, PlainTextDocumentReader>();
        services.TryAddSingleton<ZVecTextChunkerRegistry>();

        services.TryAddScoped<RagCollectionProvider>();
        services.TryAddScoped<ContextPacker>();
        services.TryAddScoped<IRagIngestor, RagIngestor>();
        services.TryAddScoped<IRagRetriever, RagRetriever>();
        services.TryAddScoped<IRagGenerator, RagGenerator>();
        services.TryAddScoped<IRagPipeline, RagPipeline>();

        return services;
    }

    /// <summary>Registers the default token-boundary chunker.</summary>
    public static IServiceCollection AddTokenChunker(
        this IServiceCollection services,
        int maxTokens = ZVec.Rag.Constants.ZVecRagConstants.DefaultChunkMaxTokens,
        int overlapTokens = ZVec.Rag.Constants.ZVecRagConstants.DefaultChunkOverlapTokens)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IZVecTextChunker, TokenTextChunker>(sp =>
        {
            var resolver = sp.GetRequiredService<ZVecTokenizerResolver>();
            return new TokenTextChunker(resolver.CreateTokenizer(), maxTokens, overlapTokens);
        }));

        return services;
    }

    /// <summary>Registers markdown heading-aware chunking.</summary>
    public static IServiceCollection AddMarkdownChunker(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IZVecTextChunker, MarkdownHeadingChunker>(sp =>
        {
            var resolver = sp.GetRequiredService<ZVecTokenizerResolver>();
            var tokenChunker = new TokenTextChunker(resolver.CreateTokenizer());
            return new MarkdownHeadingChunker(tokenChunker);
        }));

        return services;
    }

    /// <summary>Registers sentence-boundary chunking.</summary>
    public static IServiceCollection AddSentenceChunker(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IZVecTextChunker, SentenceTextChunker>());
        return services;
    }
}
