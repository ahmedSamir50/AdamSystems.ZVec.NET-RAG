namespace ZVec.Rag.Models;

/// <summary>
/// Controls ordering of retrieved context inside the LLM prompt (independent of <see cref="CitationOrder"/>).
/// </summary>
public enum ContextPackingStrategy
{
    /// <summary>Pack chunks by rank score descending.</summary>
    ScoreDescending = 0,

    /// <summary>Apply Lost-in-the-Middle reordering to the retrieved context block only.</summary>
    LostInTheMiddle = 1
}
