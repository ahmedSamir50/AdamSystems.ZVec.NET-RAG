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

        var collection = await _collectionProvider.GetCollectionAsync(cancellationToken).ConfigureAwait(false);

        if (collection is not IKeywordHybridSearchable<ZVecRagRecordV1> hybrid)
        {
            throw new InvalidOperationException("RAG collection does not support hybrid search.");
        }

        GeneratedEmbeddings<Embedding<float>> queryEmbeddings = await embedder.GenerateAsync(
            [query],
            options: null,
            cancellationToken).ConfigureAwait(false);

        ReadOnlyMemory<float> queryVector = queryEmbeddings[0].Vector;
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
            RankScore: rankScore,
            DenseScore: ComputeCosineSimilarity(queryVector, record.DenseVector),
            FtsScore: 0f);
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
