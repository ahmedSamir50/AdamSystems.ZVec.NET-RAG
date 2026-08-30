namespace ZVec.Rag.Testing.Evaluation;

/// <summary>Retrieval evaluation metrics for a single query.</summary>
/// <param name="RecallAtK">Fraction of gold chunk ids found in the top-K retrieved ids.</param>
/// <param name="Mrr">Reciprocal rank of the first relevant hit (0 when none).</param>
/// <param name="NdcgAtK">Normalized discounted cumulative gain at K.</param>
/// <param name="K">Cutoff used for Recall@K and nDCG@K.</param>
public readonly record struct RagRetrievalMetrics(double RecallAtK, double Mrr, double NdcgAtK, int K);
