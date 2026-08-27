namespace ZVec.Rag.Models;

/// <summary>
/// Controls citation list ordering for UI and API responses (independent of prompt packing order).
/// </summary>
public enum CitationOrder
{
    /// <summary>Sort by fused rank score descending (Story 2.1 default).</summary>
    ScoreDescending = 0
}
