using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using System.Threading.Channels;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;
using ZVec.Rag.Models;
using ZVec.Rag.Schema;

namespace ZVec.Rag.Ingestion;

public sealed partial class RagIngestor
{
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

    private static async Task FlushEmbedBatchAsync(
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

    private static async Task<int> ResolveStartChunkIndexAsync(
        VectorStoreCollection<string, ZVecRagRecordV1> collection,
        VectorStoreCollection<string, ZVecRagSectionSummaryV1>? summaryCollection,
        string documentId,
        DuplicateMode mode,
        CancellationToken cancellationToken)
    {
        switch (mode)
        {
            case DuplicateMode.Replace:
                await DeleteAllDocumentChunksAsync(collection, documentId, cancellationToken).ConfigureAwait(false);
                if (summaryCollection != null)
                {
                    await SectionSummaryIngestStage.DeleteAllDocumentSummariesAsync(
                        summaryCollection,
                        documentId,
                        cancellationToken).ConfigureAwait(false);
                }

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
}
