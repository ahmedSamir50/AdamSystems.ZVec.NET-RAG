namespace ZVec.Rag.Models;

/// <summary>
/// Result of a document ingestion operation.
/// </summary>
/// <param name="DocumentId">Source document identifier.</param>
/// <param name="ChunksIngested">Number of chunks written to the vector store.</param>
/// <param name="ChunkIds">Identifiers of ingested chunks.</param>
public sealed record IngestionResult(
    string DocumentId,
    int ChunksIngested,
    IReadOnlyList<string> ChunkIds);
