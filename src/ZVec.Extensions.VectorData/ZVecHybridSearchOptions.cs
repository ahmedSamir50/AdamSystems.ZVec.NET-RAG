using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData.Constants;

namespace ZVec.Extensions.VectorData;

/// <summary>
/// ZVec-specific hybrid search options. Derives from <see cref="HybridSearchOptions{TRecord}"/>
/// to add ZVec-native tuning knobs that the base abstraction does not expose.
/// </summary>
/// <typeparam name="TRecord">The record data model.</typeparam>
public sealed class ZVecHybridSearchOptions<TRecord> : HybridSearchOptions<TRecord>
{
    /// <summary>
    /// Reciprocal Rank Fusion (RRF) smoothing constant <c>k</c>. Passed to the native
    /// <c>ZVecRrfReranker</c>. Defaults to <c>60</c> (the standard RRF value).
    /// Higher values smooth rank differences; lower values amplify top-ranked results.
    /// </summary>
    public int RrfK { get; set; } = ZVecConstants.DefaultRrfRankConstant;
}
