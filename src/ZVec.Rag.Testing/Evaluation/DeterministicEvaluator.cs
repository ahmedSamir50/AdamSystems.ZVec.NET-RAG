namespace ZVec.Rag.Testing.Evaluation;

/// <summary>
/// Pure arithmetic retrieval evaluator for CI (no LLM, no native engine).
/// </summary>
public sealed class DeterministicEvaluator : IRagEvaluator
{
    /// <inheritdoc />
    public RagRetrievalMetrics EvaluateRetrieval(
        IReadOnlyList<string> goldChunkIds,
        IReadOnlyList<string> retrievedChunkIds,
        int k = 10,
        IReadOnlyDictionary<string, int>? relevanceGrades = null)
    {
        if (goldChunkIds == null)
        {
            throw new ArgumentNullException(nameof(goldChunkIds));
        }

        if (retrievedChunkIds == null)
        {
            throw new ArgumentNullException(nameof(retrievedChunkIds));
        }

        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), k, "K must be positive.");
        }

        var goldSet = new HashSet<string>(goldChunkIds, StringComparer.Ordinal);
        if (goldSet.Count == 0)
        {
            return new RagRetrievalMetrics(0d, 0d, 0d, k);
        }

        int effectiveK = Math.Min(k, retrievedChunkIds.Count);
        int hitsInTopK = 0;
        double mrr = 0d;
        for (int i = 0; i < effectiveK; i++)
        {
            if (goldSet.Contains(retrievedChunkIds[i]))
            {
                hitsInTopK++;
                if (mrr == 0d)
                {
                    mrr = 1d / (i + 1);
                }
            }
        }

        double recall = hitsInTopK / (double)goldSet.Count;
        double ndcg = ComputeNdcgAtK(retrievedChunkIds, relevanceGrades ?? BuildBinaryRelevance(goldSet), k);
        return new RagRetrievalMetrics(recall, mrr, ndcg, k);
    }

    /// <inheritdoc />
    public RecallAtKLiftResult RecallAtKLift(
        RagRetrievalMetrics baseline,
        RagRetrievalMetrics treatment,
        int k)
    {
        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), k, "K must be positive.");
        }

        double delta = treatment.RecallAtK - baseline.RecallAtK;
        double? relativeLift = baseline.RecallAtK > 0d
            ? delta / baseline.RecallAtK
            : null;

        return new RecallAtKLiftResult(baseline.RecallAtK, treatment.RecallAtK, delta, relativeLift, k);
    }

    private static Dictionary<string, int> BuildBinaryRelevance(HashSet<string> goldSet)
    {
        var grades = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string id in goldSet)
        {
            grades[id] = 1;
        }

        return grades;
    }

    private static double ComputeNdcgAtK(
        IReadOnlyList<string> retrievedChunkIds,
        IReadOnlyDictionary<string, int> relevanceGrades,
        int k)
    {
        int effectiveK = Math.Min(k, retrievedChunkIds.Count);
        double dcg = 0d;
        for (int i = 0; i < effectiveK; i++)
        {
            if (relevanceGrades.TryGetValue(retrievedChunkIds[i], out int gain) && gain > 0)
            {
                dcg += gain / Math.Log2(i + 2);
            }
        }

        var idealGrades = relevanceGrades.Values
            .Where(g => g > 0)
            .OrderByDescending(g => g)
            .Take(k)
            .ToArray();

        double idcg = 0d;
        for (int i = 0; i < idealGrades.Length; i++)
        {
            idcg += idealGrades[i] / Math.Log2(i + 2);
        }

        if (idcg <= 0d)
        {
            return 0d;
        }

        return dcg / idcg;
    }
}
