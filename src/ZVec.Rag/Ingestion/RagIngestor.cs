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

    /// <summary>Initializes a new instance.</summary>
    public RagIngestor(
        RagCollectionProvider collectionProvider,
        ZVecRagOptions ragOptions,
        ZVecTextChunkerRegistry chunkerRegistry,
        IRagDocumentReader documentReader)
    {
        _collectionProvider = collectionProvider ?? throw new ArgumentNullException(nameof(collectionProvider));
        _ragOptions = ragOptions ?? throw new ArgumentNullException(nameof(ragOptions));
        _chunkerRegistry = chunkerRegistry ?? throw new ArgumentNullException(nameof(chunkerRegistry));
        _documentReader = documentReader ?? throw new ArgumentNullException(nameof(documentReader));
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

        int startIndex = await ResolveStartChunkIndexAsync(
            collection,
            documentId,
            options?.OnDuplicate ?? DuplicateMode.Replace,
            cancellationToken).ConfigureAwait(false);

        if (startIndex < 0)
        {
            return new IngestionResult(documentId, 0, Array.Empty<string>());
        }

        string sourceHash = ZVecChunkIdGenerator.ComputeSourceHash(text);
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

        int startIndex = await ResolveStartChunkIndexAsync(
            collection,
            documentId,
            options?.OnDuplicate ?? DuplicateMode.Replace,
            cancellationToken).ConfigureAwait(false);

        if (startIndex < 0)
        {
            return new IngestionResult(documentId, 0, Array.Empty<string>());
        }

        string sourceHash = ZVecChunkIdGenerator.ComputeSourceHash(text);
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
    }

    private async ValueTask<IngestionResult> IngestChunkedTextAsync(
        string text,
        string documentId,
        IngestOptions? options,
        IZVecTextChunker chunker,
        IEmbeddingGenerator<string, Embedding<float>> embedder,
        VectorStoreCollection<string, ZVecRagRecordV1> collection,
        string sourceHash,
        int startChunkIndex,
        CancellationToken cancellationToken)
    {
        var parseChannel = IngestionChannelPump.CreateParseChannel();
        ChannelWriter<TextChunk> writer = parseChannel.Writer;

        Task<List<string>> consumerTask = ConsumeParseChannelAsync(
            parseChannel.Reader,
            documentId,
            options,
            chunker.StrategyId,
            sourceHash,
            embedder,
            collection,
            startChunkIndex,
            cancellationToken);

        try
        {
            await IngestionChannelPump.PumpChunksAsync(chunker, text, writer, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            writer.Complete();
        }

        var chunkIds = await consumerTask.ConfigureAwait(false);
        return new IngestionResult(documentId, chunkIds.Count, chunkIds);
    }

    private async Task<List<string>> ConsumeParseChannelAsync(
        ChannelReader<TextChunk> reader,
        string documentId,
        IngestOptions? options,
        string strategyId,
        string sourceHash,
        IEmbeddingGenerator<string, Embedding<float>> embedder,
        VectorStoreCollection<string, ZVecRagRecordV1> collection,
        int startChunkIndex,
        CancellationToken cancellationToken)
    {
        var chunkIds = new List<string>();
        var textBatch = new List<string>();
        var chunkMeta = new List<(TextChunk Chunk, int Index)>();
        int chunkIndex = startChunkIndex;

        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (reader.TryRead(out TextChunk? chunk))
            {
                textBatch.Add(chunk.Text);
                chunkMeta.Add((chunk, chunkIndex));
                chunkIndex++;

                if (textBatch.Count >= ZVecRagConstants.EmbedBatchSize)
                {
                    await FlushEmbedBatchAsync(
                        textBatch,
                        chunkMeta,
                        documentId,
                        options,
                        strategyId,
                        sourceHash,
                        embedder,
                        collection,
                        chunkIds,
                        cancellationToken).ConfigureAwait(false);
                    textBatch.Clear();
                    chunkMeta.Clear();
                }
            }
        }

        if (textBatch.Count > 0)
        {
            await FlushEmbedBatchAsync(
                textBatch,
                chunkMeta,
                documentId,
                options,
                strategyId,
                sourceHash,
                embedder,
                collection,
                chunkIds,
                cancellationToken).ConfigureAwait(false);
        }

        return chunkIds;
    }

    private async Task FlushEmbedBatchAsync(
        List<string> texts,
        List<(TextChunk Chunk, int Index)> meta,
        string documentId,
        IngestOptions? options,
        string strategyId,
        string sourceHash,
        IEmbeddingGenerator<string, Embedding<float>> embedder,
        VectorStoreCollection<string, ZVecRagRecordV1> collection,
        List<string> chunkIds,
        CancellationToken cancellationToken)
    {
        GeneratedEmbeddings<Embedding<float>> embeddings = await embedder.GenerateAsync(
            texts,
            options: null,
            cancellationToken).ConfigureAwait(false);

        var records = new List<ZVecRagRecordV1>();
        for (int i = 0; i < texts.Count; i++)
        {
            var (chunk, index) = meta[i];
            string sourceUri = options?.SourceUri ?? documentId;
            string chunkId = ZVecChunkIdGenerator.Compute(sourceUri, strategyId, index);

            records.Add(new ZVecRagRecordV1
            {
                ChunkId = chunkId,
                SourceDoc = documentId,
                SourceUri = sourceUri,
                SourceHash = sourceHash,
                Page = options?.Page ?? -1,
                Offset = chunk.Offset,
                ChunkIndex = index,
                Text = chunk.Text,
                DenseVector = embeddings[i].Vector
            });
            chunkIds.Add(chunkId);
        }

        for (int i = 0; i < records.Count; i += ZVecRagConstants.UpsertBatchSize)
        {
            var batch = records.Skip(i).Take(ZVecRagConstants.UpsertBatchSize).ToList();
            await collection.UpsertAsync(batch, cancellationToken).ConfigureAwait(false);
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

    private static async Task<int> ResolveStartChunkIndexAsync(
        VectorStoreCollection<string, ZVecRagRecordV1> collection,
        string documentId,
        DuplicateMode mode,
        CancellationToken cancellationToken)
    {
        switch (mode)
        {
            case DuplicateMode.Replace:
                await DeleteAllDocumentChunksAsync(collection, documentId, cancellationToken).ConfigureAwait(false);
                return 0;

            case DuplicateMode.Skip:
                bool exists = await DocumentHasChunksAsync(collection, documentId, cancellationToken).ConfigureAwait(false);
                return exists ? -1 : 0;

            case DuplicateMode.Append:
                return await GetMaxChunkIndexAsync(collection, documentId, cancellationToken).ConfigureAwait(false) + 1;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }

    private static async Task DeleteAllDocumentChunksAsync(
        VectorStoreCollection<string, ZVecRagRecordV1> collection,
        string documentId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var keys = new List<string>();
            await foreach (var record in collection.GetAsync(
                r => r.SourceDoc == documentId,
                ZVecRagConstants.DuplicateScanBatchSize,
                DuplicateScanRetrievalOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                keys.Add(record.ChunkId);
            }

            if (keys.Count == 0)
            {
                break;
            }

            await collection.DeleteAsync(keys, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<bool> DocumentHasChunksAsync(
        VectorStoreCollection<string, ZVecRagRecordV1> collection,
        string documentId,
        CancellationToken cancellationToken)
    {
        await foreach (var _ in collection.GetAsync(
            r => r.SourceDoc == documentId,
            1,
            DuplicateScanRetrievalOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        return false;
    }

    private static async Task<int> GetMaxChunkIndexAsync(
        VectorStoreCollection<string, ZVecRagRecordV1> collection,
        string documentId,
        CancellationToken cancellationToken)
    {
        int maxIndex = -1;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (true)
        {
            bool foundNew = false;
            await foreach (var record in collection.GetAsync(
                r => r.SourceDoc == documentId,
                ZVecRagConstants.DuplicateScanBatchSize,
                DuplicateScanRetrievalOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (seen.Add(record.ChunkId))
                {
                    foundNew = true;
                    maxIndex = Math.Max(maxIndex, record.ChunkIndex);
                }
            }

            if (!foundNew)
            {
                break;
            }
        }

        return maxIndex;
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
