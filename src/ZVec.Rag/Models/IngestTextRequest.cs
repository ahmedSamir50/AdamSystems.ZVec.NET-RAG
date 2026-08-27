namespace ZVec.Rag.Models;

/// <summary>
/// A single text ingest request for batch ingestion.
/// </summary>
/// <param name="Text">Document text to ingest.</param>
/// <param name="DocumentId">Stable document identifier.</param>
/// <param name="Options">Optional ingest metadata.</param>
public sealed record IngestTextRequest(
    string Text,
    string DocumentId,
    IngestOptions? Options = null);
