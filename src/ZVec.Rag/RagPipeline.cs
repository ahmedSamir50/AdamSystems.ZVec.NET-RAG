using ZVec.Rag.Abstractions;

namespace ZVec.Rag;

/// <summary>
/// Composite facade delegating to scoped ingest, retrieve, and generate services.
/// </summary>
public sealed class RagPipeline : IRagPipeline
{
    private readonly IRagIngestor _ingestor;
    private readonly IRagRetriever _retriever;
    private readonly IRagGenerator _generator;

    /// <summary>Initializes a new instance.</summary>
    public RagPipeline(IRagIngestor ingestor, IRagRetriever retriever, IRagGenerator generator)
    {
        _ingestor = ingestor ?? throw new ArgumentNullException(nameof(ingestor));
        _retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
    }

    /// <inheritdoc />
    public ValueTask<Models.IngestionResult> IngestTextAsync(
        string text,
        string documentId,
        Models.IngestOptions? options = null,
        CancellationToken cancellationToken = default)
        => _ingestor.IngestTextAsync(text, documentId, options, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Models.IngestionResult> IngestDocumentAsync(
        Stream documentStream,
        string documentId,
        string contentType,
        Models.IngestOptions? options = null,
        CancellationToken cancellationToken = default)
        => _ingestor.IngestDocumentAsync(documentStream, documentId, contentType, options, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Models.IngestionResult> IngestBatchAsync(
        IEnumerable<Models.IngestTextRequest> requests,
        Models.IngestOptions? options = null,
        CancellationToken cancellationToken = default)
        => _ingestor.IngestBatchAsync(requests, options, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Models.Citation>> RetrieveAsync(
        string query,
        int? topK = null,
        CancellationToken cancellationToken = default)
        => _retriever.RetrieveAsync(query, topK, cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<Models.RagChunk> AskAsync(
        string question,
        IList<Microsoft.Extensions.AI.ChatMessage>? history = null,
        bool streamCitations = true,
        CancellationToken cancellationToken = default)
        => _generator.AskAsync(question, history, streamCitations, cancellationToken);
}
