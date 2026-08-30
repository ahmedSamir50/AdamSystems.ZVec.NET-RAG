using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData.Hybrid;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;
using ZVec.Rag.Internal;
using ZVec.Rag.Models;
using ZVec.Rag.Options;
using ZVec.Rag.Schema;

namespace ZVec.Rag.Retrieval;

/// <summary>
/// Hybrid dense + FTS retrieval over the RAG chunk collection.
/// </summary>
public sealed class RagRetriever : IRagRetriever
{
    private static readonly FilteredRecordRetrievalOptions<ZVecRagRecordV1> ExpandChildRetrievalOptions =
        new() { IncludeVectors = true };

    private readonly RagCollectionProvider _collectionProvider;
    private readonly ZVecRagOptions _ragOptions;

    /// <summary>Initializes a new instance.</summary>
    public RagRetriever(RagCollectionProvider collectionProvider, ZVecRagOptions ragOptions)
    {
        _collectionProvider = collectionProvider ?? throw new ArgumentNullException(nameof(collectionProvider));
        _ragOptions = ragOptions ?? throw new ArgumentNullException(nameof(ragOptions));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Citation>> RetrieveAsync(
        string query,
        int? topK = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException(ZVecRagErrorMessages.NullOrEmptyQuestion(), nameof(query));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var embedder = _ragOptions.Embedder
            ?? throw new InvalidOperationException(ZVecRagErrorMessages.EmbedderNotConfigured());

        if (!_ragOptions.GenerateSummaries)
        {
            return await RetrieveChunksOnlyAsync(query, embedder, topK, cancellationToken).ConfigureAwait(false);
        }

        return await RetrieveWithSummariesAsync(query, embedder, topK, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<Citation>> RetrieveChunksOnlyAsync(
        string query,
        IEmbeddingGenerator<string, Embedding<float>> embedder,
        int? topK,
        CancellationToken cancellationToken)
    {
        var collection = await _collectionProvider.GetCollectionAsync(cancellationToken).ConfigureAwait(false);

        if (collection is not IKeywordHybridSearchable<ZVecRagRecordV1> hybrid)
        {
            throw new InvalidOperationException("RAG collection does not support hybrid search.");
        }

        ReadOnlyMemory<float> queryVector = await EncodeQueryAsync(query, embedder, cancellationToken).ConfigureAwait(false);
        int effectiveTop = topK ?? _ragOptions.RetrieveTopK;
        string[] keywords = TokenizeKeywords(query);

        var hybridOptions = new ZVecHybridSearchOptions<ZVecRagRecordV1>
        {
            RrfK = _ragOptions.RrfK,
            IncludeVectors = true
        };

        var citations = new List<Citation>();
        await foreach (var result in hybrid.HybridSearchAsync(
            queryVector,
            keywords,
            effectiveTop,
            hybridOptions,
            cancellationToken).ConfigureAwait(false))
        {
            citations.Add(MapToCitation(result.Record, (float)(result.Score ?? 0d), queryVector));
        }

        return SortCitations(citations, _ragOptions.CitationOrder);
    }

    private async Task<IReadOnlyList<Citation>> RetrieveWithSummariesAsync(
        string query,
        IEmbeddingGenerator<string, Embedding<float>> embedder,
        int? topK,
        CancellationToken cancellationToken)
    {
        var chunkCollection = await _collectionProvider.GetCollectionAsync(cancellationToken).ConfigureAwait(false);
        var summaryCollection = await _collectionProvider.GetSummaryCollectionAsync(cancellationToken).ConfigureAwait(false);

        if (chunkCollection is not IKeywordHybridSearchable<ZVecRagRecordV1> chunkHybrid)
        {
            throw new InvalidOperationException("RAG collection does not support hybrid search.");
        }

        if (summaryCollection is not IKeywordHybridSearchable<ZVecRagSectionSummaryV1> summaryHybrid)
        {
            throw new InvalidOperationException("Section summary collection does not support hybrid search.");
        }

        ReadOnlyMemory<float> queryVector = await EncodeQueryAsync(query, embedder, cancellationToken).ConfigureAwait(false);
        int effectiveTop = topK ?? _ragOptions.RetrieveTopK;
        string[] keywords = TokenizeKeywords(query);

        var chunkHybridOptions = new ZVecHybridSearchOptions<ZVecRagRecordV1>
        {
            RrfK = _ragOptions.RrfK,
            IncludeVectors = true
        };

        var summaryHybridOptions = new ZVecHybridSearchOptions<ZVecRagSectionSummaryV1>
        {
            RrfK = _ragOptions.RrfK,
            IncludeVectors = true
        };

        Task<List<Citation>> chunkTask = CollectChunkHitsAsync(
            chunkHybrid,
            queryVector,
            keywords,
            effectiveTop,
            chunkHybridOptions,
            cancellationToken);

        Task<List<(ZVecRagSectionSummaryV1 Summary, float Score)>> summaryTask = CollectSummaryHitsAsync(
            summaryHybrid,
            queryVector,
            keywords,
            effectiveTop,
            summaryHybridOptions,
            cancellationToken);

        await Task.WhenAll(chunkTask, summaryTask).ConfigureAwait(false);

        List<Citation> chunkHits = await chunkTask.ConfigureAwait(false);
        List<(ZVecRagSectionSummaryV1 Summary, float Score)> summaryHits = await summaryTask.ConfigureAwait(false);

        var expandedChildren = new List<ZVecRagRecordV1>();
        foreach ((ZVecRagSectionSummaryV1 summary, _) in summaryHits
                     .OrderByDescending(h => h.Score)
                     .Take(ZVecRagConstants.DefaultSummaryExpandTopS))
        {
            await foreach (var child in chunkCollection.GetAsync(
                r => r.SectionSummaryId == summary.SectionSummaryId,
                effectiveTop,
                ExpandChildRetrievalOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                expandedChildren.Add(child);
            }
        }

        List<Citation> fused = SectionSummaryFusion.Fuse(
            chunkHits,
            summaryHits,
            expandedChildren,
            queryVector,
            ZVecRagConstants.DefaultSummaryParentBoost);

        return SortCitations(fused, _ragOptions.CitationOrder);
    }

    private static async Task<List<Citation>> CollectChunkHitsAsync(
        IKeywordHybridSearchable<ZVecRagRecordV1> hybrid,
        ReadOnlyMemory<float> queryVector,
        string[] keywords,
        int effectiveTop,
        ZVecHybridSearchOptions<ZVecRagRecordV1> hybridOptions,
        CancellationToken cancellationToken)
    {
        var citations = new List<Citation>();
        await foreach (var result in hybrid.HybridSearchAsync(
            queryVector,
            keywords,
            effectiveTop,
            hybridOptions,
            cancellationToken).ConfigureAwait(false))
        {
            citations.Add(MapToCitation(result.Record, (float)(result.Score ?? 0d), queryVector));
        }

        return citations;
    }

    private static async Task<List<(ZVecRagSectionSummaryV1 Summary, float Score)>> CollectSummaryHitsAsync(
        IKeywordHybridSearchable<ZVecRagSectionSummaryV1> hybrid,
        ReadOnlyMemory<float> queryVector,
        string[] keywords,
        int effectiveTop,
        ZVecHybridSearchOptions<ZVecRagSectionSummaryV1> hybridOptions,
        CancellationToken cancellationToken)
    {
        var hits = new List<(ZVecRagSectionSummaryV1, float)>();
        await foreach (var result in hybrid.HybridSearchAsync(
            queryVector,
            keywords,
            effectiveTop,
            hybridOptions,
            cancellationToken).ConfigureAwait(false))
        {
            hits.Add((result.Record, (float)(result.Score ?? 0d)));
        }

        return hits;
    }

    private static async Task<ReadOnlyMemory<float>> EncodeQueryAsync(
        string query,
        IEmbeddingGenerator<string, Embedding<float>> embedder,
        CancellationToken cancellationToken)
    {
        GeneratedEmbeddings<Embedding<float>> queryEmbeddings = await embedder.GenerateAsync(
            [query],
            options: null,
            cancellationToken).ConfigureAwait(false);

        return queryEmbeddings[0].Vector;
    }

    public static IReadOnlyList<Citation> SortCitations(IReadOnlyList<Citation> citations, CitationOrder order)
    {
        return order switch
        {
            CitationOrder.ScoreDescending => citations.OrderByDescending(c => c.RankScore).ToList(),
            CitationOrder.ChunkOrderAscending => citations.OrderBy(c => c.ChunkIndex).ToList(),
            CitationOrder.SourceDocThenChunkOrder => citations
                .OrderBy(c => c.SourceDoc, StringComparer.Ordinal)
                .ThenBy(c => c.ChunkIndex)
                .ToList(),
            CitationOrder.PageAscending => citations
                .OrderBy(c => c.Page ?? int.MaxValue)
                .ThenBy(c => c.ChunkIndex)
                .ToList(),
            CitationOrder.None => citations.ToList(),
            _ => citations.OrderByDescending(c => c.RankScore).ToList()
        };
    }

    private static Citation MapToCitation(
        ZVecRagRecordV1 record,
        float rankScore,
        ReadOnlyMemory<float> queryVector)
    {
        return new Citation(
            record.SourceDoc,
            record.SourceUri,
            record.SourceHash,
            record.Page < 0 ? null : record.Page,
            record.Offset,
            record.ChunkIndex,
            record.ChunkId,
            record.Text,
            rankScore,
            ComputeCosineSimilarity(queryVector, record.DenseVector),
            0f,
            record.SectionSummaryId);
    }

    public static float ComputeCosineSimilarity(ReadOnlyMemory<float> left, ReadOnlyMemory<float> right)
    {
        if (left.IsEmpty || right.IsEmpty || left.Length != right.Length)
        {
            return 0f;
        }

        ReadOnlySpan<float> leftSpan = left.Span;
        ReadOnlySpan<float> rightSpan = right.Span;
        double dot = 0d;
        double leftMag = 0d;
        double rightMag = 0d;
        for (int i = 0; i < leftSpan.Length; i++)
        {
            dot += leftSpan[i] * rightSpan[i];
            leftMag += leftSpan[i] * leftSpan[i];
            rightMag += rightSpan[i] * rightSpan[i];
        }

        if (leftMag <= 0d || rightMag <= 0d)
        {
            return 0f;
        }

        double cosine = dot / (Math.Sqrt(leftMag) * Math.Sqrt(rightMag));
        return (float)Math.Clamp(cosine, 0d, 1d);
    }

    private static string[] TokenizeKeywords(string query)
    {
        return query.Split(
            [' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
