namespace ZVec.Rag.Testing.Evaluation;

/// <summary>
/// Computes retrieval quality metrics for ranked chunk identifiers.
/// </summary>
public interface IRagEvaluator
{
    /// <summary>Computes Recall@K, MRR, and nDCG@K for a ranked retrieval list.</summary>
    RagRetrievalMetrics EvaluateRetrieval(
        IReadOnlyList<string> goldChunkIds,
        IReadOnlyList<string> retrievedChunkIds,
        int k = 10,
        IReadOnlyDictionary<string, int>? relevanceGrades = null);

    /// <summary>
    /// Compares baseline vs treatment Recall@K.
    /// Relative lift is null when baseline Recall@K is zero.
    /// </summary>
    RecallAtKLiftResult RecallAtKLift(
        RagRetrievalMetrics baseline,
        RagRetrievalMetrics treatment,
        int k);
}
