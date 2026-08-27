using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;
using ZVec.Rag.Ingestion;
using ZVec.Rag.Internal;
using ZVec.Rag.Models;
using ZVec.Rag.Options;
using ZVec.Rag.Schema;

namespace ZVec.Rag.Ingestion;

/// <summary>
/// Thin Story 2.1 ingestor: whole-text single-chunk upsert via the real ZVec connector.
/// </summary>
public sealed class RagIngestor : IRagIngestor
{
    private readonly RagCollectionProvider _collectionProvider;
    private readonly ZVecRagOptions _ragOptions;

    /// <summary>Initializes a new instance.</summary>
    public RagIngestor(RagCollectionProvider collectionProvider, ZVecRagOptions ragOptions)
    {
        _collectionProvider = collectionProvider ?? throw new ArgumentNullException(nameof(collectionProvider));
        _ragOptions = ragOptions ?? throw new ArgumentNullException(nameof(ragOptions));
    }

    /// <inheritdoc />
    public async ValueTask<IngestionResult> IngestTextAsync(
        string text,
        string documentId,
        IngestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ValidateTextAndDocumentId(text, documentId);
        cancellationToken.ThrowIfCancellationRequested();

        var embedder = RequireEmbedder();
        var collection = await _collectionProvider.GetCollectionAsync(cancellationToken).ConfigureAwait(false);

        var record = await BuildRecordAsync(
            text,
            documentId,
            chunkIndex: 0,
            options,
            embedder,
            cancellationToken).ConfigureAwait(false);

        await collection.UpsertAsync(record, cancellationToken).ConfigureAwait(false);
        return new IngestionResult(documentId, 1, new[] { record.ChunkId });
    }

    /// <inheritdoc />
    public async ValueTask<IngestionResult> IngestDocumentAsync(
        Stream documentStream,
        string documentId,
        string contentType,
        IngestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (documentStream == null)
        {
            throw new ArgumentNullException(nameof(documentStream));
        }

        ValidateDocumentId(documentId);
        ValidateContentType(contentType);

        using var reader = new StreamReader(documentStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        string text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return await IngestTextAsync(text, documentId, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IngestionResult> IngestBatchAsync(
        IEnumerable<IngestTextRequest> requests,
        IngestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (requests == null)
        {
            throw new ArgumentNullException(nameof(requests));
        }

        var allChunkIds = new List<string>();
        string? lastDocumentId = null;
        int totalChunks = 0;

        foreach (var request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await IngestTextAsync(
                request.Text,
                request.DocumentId,
                request.Options ?? options,
                cancellationToken).ConfigureAwait(false);

            lastDocumentId = result.DocumentId;
            totalChunks += result.ChunksIngested;
            allChunkIds.AddRange(result.ChunkIds);
        }

        return new IngestionResult(lastDocumentId ?? string.Empty, totalChunks, allChunkIds);
    }

    private async Task<ZVecRagRecordV1> BuildRecordAsync(
        string text,
        string documentId,
        int chunkIndex,
        IngestOptions? options,
        IEmbeddingGenerator<string, Embedding<float>> embedder,
        CancellationToken cancellationToken)
    {
        string sourceUri = options?.SourceUri ?? documentId;
        string strategyId = ZVecChunkIdGenerator.DefaultStrategyId;
        string chunkId = ZVecChunkIdGenerator.Compute(sourceUri, strategyId, chunkIndex);
        string sourceHash = ZVecChunkIdGenerator.ComputeSourceHash(text);

        GeneratedEmbeddings<Embedding<float>> embeddings = await embedder.GenerateAsync(
            [text],
            options: null,
            cancellationToken).ConfigureAwait(false);

        Embedding<float> embedding = embeddings[0];
        ReadOnlyMemory<float> vector = embedding.Vector;

        return new ZVecRagRecordV1
        {
            ChunkId = chunkId,
            SourceDoc = documentId,
            SourceUri = sourceUri,
            SourceHash = sourceHash,
            Page = options?.Page ?? -1,
            Offset = 0,
            ChunkIndex = chunkIndex,
            Text = text,
            DenseVector = vector
        };
    }

    private IEmbeddingGenerator<string, Embedding<float>> RequireEmbedder()
    {
        return _ragOptions.Embedder
            ?? throw new InvalidOperationException(ZVecRagErrorMessages.EmbedderNotConfigured());
    }

    private static void ValidateTextAndDocumentId(string text, string documentId)
    {
        ValidateDocumentId(documentId);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(ZVecRagErrorMessages.NullOrEmptyText(), nameof(text));
        }
    }

    private static void ValidateDocumentId(string documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException(ZVecRagErrorMessages.NullOrEmptyDocumentId(), nameof(documentId));
        }
    }

    private static void ValidateContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException(ZVecRagErrorMessages.UnsupportedContentType(contentType), nameof(contentType));
        }

        if (!contentType.Equals(ZVecRagConstants.PlainTextContentType, StringComparison.OrdinalIgnoreCase)
            && !contentType.Equals(ZVecRagConstants.MarkdownContentType, StringComparison.OrdinalIgnoreCase)
            && !contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(ZVecRagErrorMessages.UnsupportedContentType(contentType));
        }
    }
}
