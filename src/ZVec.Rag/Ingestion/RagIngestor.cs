using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using System.Threading.Channels;
using ZVec.Extensions.VectorData.Collection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;
using ZVec.Rag.Internal;
using ZVec.Rag.Models;
using ZVec.Rag.Options;
using ZVec.Rag.Schema;
using ZVec.Rag.Security;

namespace ZVec.Rag.Ingestion;

/// <summary>
/// Channel-based document ingestion with deduplication and batch embedding.
/// </summary>
public sealed partial class RagIngestor : IRagIngestor
{
    private static readonly FilteredRecordRetrievalOptions<ZVecRagRecordV1> DuplicateScanRetrievalOptions =
        new() { IncludeVectors = false };

    private readonly RagCollectionProvider _collectionProvider;
    private readonly ZVecRagOptions _ragOptions;
    private readonly ZVecTextChunkerRegistry _chunkerRegistry;
    private readonly IRagDocumentReader _documentReader;
    private readonly SectionSummaryIngestStage _sectionSummaryStage;

    /// <summary>Initializes a new instance.</summary>
    public RagIngestor(
        RagCollectionProvider collectionProvider,
        ZVecRagOptions ragOptions,
        ZVecTextChunkerRegistry chunkerRegistry,
        IRagDocumentReader documentReader,
        ZVecTokenizerResolver tokenizerResolver,
        IRagSecuritySanitizer? securitySanitizer = null)
    {
        _collectionProvider = collectionProvider ?? throw new ArgumentNullException(nameof(collectionProvider));
        _ragOptions = ragOptions ?? throw new ArgumentNullException(nameof(ragOptions));
        _chunkerRegistry = chunkerRegistry ?? throw new ArgumentNullException(nameof(chunkerRegistry));
        _documentReader = documentReader ?? throw new ArgumentNullException(nameof(documentReader));
        _sectionSummaryStage = new SectionSummaryIngestStage(
            ragOptions,
            tokenizerResolver ?? throw new ArgumentNullException(nameof(tokenizerResolver)),
            securitySanitizer ?? ragOptions.SecuritySanitizer ?? new DefaultRagSecuritySanitizer());
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
        var chunker = ResolveChunker(options, isMarkdown: false);

        VectorStoreCollection<string, ZVecRagSectionSummaryV1>? summaryCollection = null;
        if (options?.GenerateSummaries == true)
        {
            summaryCollection = await _collectionProvider.GetSummaryCollectionAsync(cancellationToken).ConfigureAwait(false);
        }

        int startIndex = await ResolveStartChunkIndexAsync(
            collection,
            summaryCollection,
            documentId,
            options?.OnDuplicate ?? DuplicateMode.Replace,
            cancellationToken).ConfigureAwait(false);

        if (startIndex < 0)
        {
            return new IngestionResult(documentId, 0, Array.Empty<string>());
        }

        string sourceHash = ZVecChunkIdGenerator.ComputeSourceHash(text);

        if (options?.GenerateSummaries == true && summaryCollection != null)
        {
            return await _sectionSummaryStage.IngestAsync(
                text,
                documentId,
                options,
                chunker,
                embedder,
                collection,
                summaryCollection,
                sourceHash,
                startIndex,
                cancellationToken).ConfigureAwait(false);
        }

        return await IngestChunkedTextAsync(
            text,
            documentId,
            options,
            chunker,
            embedder,
            collection,
            sourceHash,
            startIndex,
            cancellationToken).ConfigureAwait(false);
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

        string text = await _documentReader.ReadAsync(documentStream, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(ZVecRagErrorMessages.NullOrEmptyText(), nameof(documentStream));
        }

        bool isMarkdown = contentType.Equals(ZVecRagConstants.MarkdownContentType, StringComparison.OrdinalIgnoreCase);
        var embedder = RequireEmbedder();
        var collection = await _collectionProvider.GetCollectionAsync(cancellationToken).ConfigureAwait(false);
        var chunker = ResolveChunker(options, isMarkdown);

        VectorStoreCollection<string, ZVecRagSectionSummaryV1>? summaryCollection = null;
        if (options?.GenerateSummaries == true)
        {
            summaryCollection = await _collectionProvider.GetSummaryCollectionAsync(cancellationToken).ConfigureAwait(false);
        }

        int startIndex = await ResolveStartChunkIndexAsync(
            collection,
            summaryCollection,
            documentId,
            options?.OnDuplicate ?? DuplicateMode.Replace,
            cancellationToken).ConfigureAwait(false);

        if (startIndex < 0)
        {
            return new IngestionResult(documentId, 0, Array.Empty<string>());
        }

        string sourceHash = ZVecChunkIdGenerator.ComputeSourceHash(text);

        if (options?.GenerateSummaries == true && summaryCollection != null)
        {
            return await _sectionSummaryStage.IngestAsync(
                text,
                documentId,
                options,
                chunker,
                embedder,
                collection,
                summaryCollection,
                sourceHash,
                startIndex,
                cancellationToken).ConfigureAwait(false);
        }

        return await IngestChunkedTextAsync(
            text,
            documentId,
            options,
            chunker,
            embedder,
            collection,
            sourceHash,
            startIndex,
            cancellationToken).ConfigureAwait(false);
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

        await OptimizeAsync(cancellationToken).ConfigureAwait(false);
        return new IngestionResult(lastDocumentId ?? string.Empty, totalChunks, allChunkIds);
    }

    /// <inheritdoc />
    public async Task OptimizeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var collection = await _collectionProvider.GetCollectionAsync(cancellationToken).ConfigureAwait(false);

        if (collection is ZVecVectorizableRecordCollection<ZVecRagRecordV1, string> zvecCollection)
        {
            await zvecCollection.OptimizeAndReopenAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_collectionProvider.SummaryCollectionOpened)
        {
            var summaryCollection = await _collectionProvider.GetSummaryCollectionAsync(cancellationToken).ConfigureAwait(false);
            if (summaryCollection is ZVecVectorizableRecordCollection<ZVecRagSectionSummaryV1, string> summaryNative)
            {
                await summaryNative.OptimizeAndReopenAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private IZVecTextChunker ResolveChunker(IngestOptions? options, bool isMarkdown)
    {
        if (options?.Chunker != null)
        {
            return options.Chunker;
        }

        if (isMarkdown)
        {
            IZVecTextChunker? markdown = _chunkerRegistry.GetMarkdownChunker();
            if (markdown != null)
            {
                return markdown;
            }
        }

        return _chunkerRegistry.GetDefault();
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
