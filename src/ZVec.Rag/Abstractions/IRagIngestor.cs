using Microsoft.Extensions.AI;
using ZVec.Rag.Models;

namespace ZVec.Rag.Abstractions;

/// <summary>
/// Document ingestion and chunk persistence contract.
/// </summary>
public interface IRagIngestor
{
    /// <summary>Ingests a plain-text or markdown document as a single chunk (Story 2.1 thin path).</summary>
    ValueTask<IngestionResult> IngestTextAsync(
        string text,
        string documentId,
        IngestOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Ingests a UTF-8 text or markdown stream.</summary>
    ValueTask<IngestionResult> IngestDocumentAsync(
        Stream documentStream,
        string documentId,
        string contentType,
        IngestOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Sequentially ingests multiple text documents.</summary>
    ValueTask<IngestionResult> IngestBatchAsync(
        IEnumerable<IngestTextRequest> requests,
        IngestOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Optimizes the native index and reopens the collection handle.</summary>
    Task OptimizeAsync(CancellationToken cancellationToken = default);
}
