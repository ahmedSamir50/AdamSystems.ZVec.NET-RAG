namespace ZVec.Rag.Models;

/// <summary>
/// Controls citation list ordering for UI and API responses (independent of prompt packing order).
/// </summary>
public enum CitationOrder
{
    /// <summary>Sort by fused rank score descending (default).</summary>
    ScoreDescending = 0,

    /// <summary>Sort by chunk index ascending within each document.</summary>
    ChunkOrderAscending = 1,

    /// <summary>Sort by source document id then chunk index.</summary>
    SourceDocThenChunkOrder = 2,

    /// <summary>Sort by page number ascending (null pages last).</summary>
    PageAscending = 3,

    /// <summary>Preserve retrieval order without sorting.</summary>
    None = 4
}
