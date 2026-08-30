using ZVec.Rag.Constants;
using ZVec.Rag.Models;
using ZVec.Rag.Schema;

namespace ZVec.Rag.Retrieval;

/// <summary>
/// Merges chunk and section-summary hybrid hits with parent boost and child expansion.
/// </summary>
internal static class SectionSummaryFusion
{
    /// <summary>
    /// Unions chunk hits with boosted scores and children of top matching summaries.
    /// </summary>
    public static List<Citation> Fuse(
        IReadOnlyList<Citation> chunkHits,
        IReadOnlyList<(ZVecRagSectionSummaryV1 Summary, float Score)> summaryHits,
        IReadOnlyList<ZVecRagRecordV1> expandedChildren,
        ReadOnlyMemory<float> queryVector,
        float parentBoost)
    {
        var summaryById = summaryHits.ToDictionary(h => h.Summary.SectionSummaryId, h => h, StringComparer.Ordinal);
        var byChunkId = new Dictionary<string, Citation>(StringComparer.Ordinal);

        foreach (Citation citation in chunkHits)
        {
            float rank = citation.RankScore;
            if (!string.IsNullOrEmpty(citation.SectionSummaryId)
                && summaryById.ContainsKey(citation.SectionSummaryId))
            {
                rank += parentBoost;
            }

            string summaryText = string.Empty;
            if (!string.IsNullOrEmpty(citation.SectionSummaryId)
                && summaryById.TryGetValue(citation.SectionSummaryId, out var parent))
            {
                summaryText = parent.Summary.Summary;
            }

            byChunkId[citation.ChunkId] = citation with
            {
                RankScore = rank,
                SectionSummary = summaryText
            };
        }

        foreach (ZVecRagRecordV1 child in expandedChildren)
        {
            if (byChunkId.ContainsKey(child.ChunkId))
            {
                continue;
            }

            float rank = 0f;
            string summaryText = string.Empty;
            if (!string.IsNullOrEmpty(child.SectionSummaryId)
                && summaryById.TryGetValue(child.SectionSummaryId, out var parent))
            {
                rank = parent.Score;
                summaryText = parent.Summary.Summary;
            }

            byChunkId[child.ChunkId] = new Citation(
                child.SourceDoc,
                child.SourceUri,
                child.SourceHash,
                child.Page < 0 ? null : child.Page,
                child.Offset,
                child.ChunkIndex,
                child.ChunkId,
                child.Text,
                rank,
                RagRetriever.ComputeCosineSimilarity(queryVector, child.DenseVector),
                0f,
                child.SectionSummaryId,
                summaryText);
        }

        return byChunkId.Values.ToList();
    }
}
