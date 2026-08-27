namespace ZVec.Rag.Models;

/// <summary>
/// A retrieved source citation with distinct rank, dense, and FTS scores.
/// </summary>
/// <param name="SourceDoc">Stable document identifier.</param>
/// <param name="SourceUri">Display URI or file path.</param>
/// <param name="SourceHash">Content hash for deduplication.</param>
/// <param name="Page">Optional page number.</param>
/// <param name="Offset">Character offset in extracted text.</param>
/// <param name="ChunkIndex">0-based chunk index within the document.</param>
/// <param name="ChunkId">Content-addressable chunk identifier.</param>
/// <param name="Text">Chunk text content.</param>
/// <param name="RankScore">Fused RRF rank score used for sorting.</param>
/// <param name="DenseScore">Normalized cosine similarity (0–1).</param>
/// <param name="FtsScore">Raw FTS relevance score.</param>
public sealed record Citation(
    string SourceDoc,
    string SourceUri,
    string SourceHash,
    int? Page,
    long Offset,
    int ChunkIndex,
    string ChunkId,
    string Text,
    float RankScore,
    float DenseScore,
    float FtsScore);
