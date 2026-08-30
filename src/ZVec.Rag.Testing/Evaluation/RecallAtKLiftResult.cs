namespace ZVec.Rag.Testing.Evaluation;

/// <summary>Paired Recall@K lift between baseline and treatment runs.</summary>
/// <param name="Baseline">Baseline Recall@K.</param>
/// <param name="Treatment">Treatment Recall@K.</param>
/// <param name="Delta">Treatment minus baseline.</param>
/// <param name="RelativeLift">(Treatment - Baseline) / Baseline when baseline &gt; 0; otherwise null.</param>
/// <param name="K">Cutoff used for Recall@K.</param>
public readonly record struct RecallAtKLiftResult(
    double Baseline,
    double Treatment,
    double Delta,
    double? RelativeLift,
    int K);
