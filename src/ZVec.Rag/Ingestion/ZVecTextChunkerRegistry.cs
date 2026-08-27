using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;

namespace ZVec.Rag.Ingestion;

/// <summary>
/// Resolves registered <see cref="IZVecTextChunker"/> instances from DI (no reflection).
/// </summary>
public sealed class ZVecTextChunkerRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, IZVecTextChunker> _byStrategyId;

    /// <summary>Initializes a new instance.</summary>
    public ZVecTextChunkerRegistry(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _byStrategyId = new Dictionary<string, IZVecTextChunker>(StringComparer.Ordinal);

        foreach (var chunker in serviceProvider.GetServices<IZVecTextChunker>())
        {
            _byStrategyId[chunker.StrategyId] = chunker;
        }
    }

    /// <summary>Gets the default token chunker.</summary>
    public IZVecTextChunker GetDefault()
    {
        return GetByStrategyId(ZVecRagConstants.TokenChunkerStrategyId)
            ?? throw new InvalidOperationException("Default TokenTextChunker is not registered. Call AddTokenChunker during AddZVecRag setup.");
    }

    /// <summary>Gets markdown heading chunker when registered.</summary>
    public IZVecTextChunker? GetMarkdownChunker()
        => GetByStrategyId(ZVecRagConstants.MarkdownHeadingChunkerStrategyId);

    /// <summary>Gets sentence chunker when registered.</summary>
    public IZVecTextChunker? GetSentenceChunker()
        => GetByStrategyId(ZVecRagConstants.SentenceChunkerStrategyId);

    /// <inheritdoc cref="GetByStrategyId"/>
    public IZVecTextChunker? GetByStrategyId(string strategyId)
        => _byStrategyId.TryGetValue(strategyId, out var chunker) ? chunker : null;
}
