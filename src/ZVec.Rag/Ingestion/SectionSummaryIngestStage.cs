using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using System.Threading.Channels;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;
using ZVec.Rag.Ingestion;
using ZVec.Rag.Models;
using ZVec.Rag.Options;
using ZVec.Rag.Schema;
using ZVec.Rag.Security;
using ZVec.Rag.Telemetry;

namespace ZVec.Rag.Ingestion;

/// <summary>
/// Bounded-channel ingest stage: section split → LLM summary → embed summary → chunk section → embed chunks.
/// </summary>
internal sealed class SectionSummaryIngestStage
{
    private static readonly FilteredRecordRetrievalOptions<ZVecRagSectionSummaryV1> SummaryScanOptions =
        new() { IncludeVectors = false };

    private readonly ZVecRagOptions _ragOptions;
    private readonly ZVecTokenizerResolver _tokenizerResolver;
    private readonly IRagSecuritySanitizer _securitySanitizer;

    /// <summary>Initializes a new instance.</summary>
    public SectionSummaryIngestStage(
        ZVecRagOptions ragOptions,
        ZVecTokenizerResolver tokenizerResolver,
        IRagSecuritySanitizer securitySanitizer)
    {
        _ragOptions = ragOptions ?? throw new ArgumentNullException(nameof(ragOptions));
        _tokenizerResolver = tokenizerResolver ?? throw new ArgumentNullException(nameof(tokenizerResolver));
        _securitySanitizer = securitySanitizer ?? throw new ArgumentNullException(nameof(securitySanitizer));
    }

    /// <summary>
    /// Ingests text with per-section summaries into chunk and summary collections.
    /// </summary>
    public async ValueTask<IngestionResult> IngestAsync(
        string text,
        string documentId,
        IngestOptions options,
        IZVecTextChunker chunker,
        IEmbeddingGenerator<string, Embedding<float>> embedder,
        VectorStoreCollection<string, ZVecRagRecordV1> chunkCollection,
        VectorStoreCollection<string, ZVecRagSectionSummaryV1> summaryCollection,
        string sourceHash,
        int startChunkIndex,
        CancellationToken cancellationToken)
    {
        var chat = _ragOptions.Chat
            ?? throw new InvalidOperationException(ZVecRagErrorMessages.ChatClientNotConfigured());

        var sectionSplitter = new TokenTextChunker(
            _tokenizerResolver.CreateTokenizer(),
            options.SummarySectionMaxTokens,
            overlapTokens: 0);

        var sectionChannel = Channel.CreateBounded<SectionWorkItem>(
            new BoundedChannelOptions(ZVecRagConstants.ParseChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });

        string sourceUri = options.SourceUri ?? documentId;
        int sectionIndex = 0;
        foreach (TextChunk sectionChunk in sectionSplitter.Chunk(text))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string sectionSummaryId = ZVecChunkIdGenerator.ComputeSectionSummaryId(sourceUri, sectionIndex);
            await sectionChannel.Writer.WriteAsync(
                new SectionWorkItem(sectionIndex, sectionChunk.Text, sectionSummaryId),
                cancellationToken).ConfigureAwait(false);
            sectionIndex++;
        }

        sectionChannel.Writer.Complete();

        var chunkIds = new List<string>();
        int nextChunkIndex = startChunkIndex;
        await foreach (SectionWorkItem item in sectionChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            string sanitizedSection = _securitySanitizer.SanitizeChunk(item.SectionText);
            string summaryText = await SummarizeSectionAsync(chat, sanitizedSection, options.MaxSummaryTokens, cancellationToken)
                .ConfigureAwait(false);
            string sanitizedSummary = _securitySanitizer.SanitizeChunk(summaryText);

            GeneratedEmbeddings<Embedding<float>> summaryEmbedding = await embedder.GenerateAsync(
                [sanitizedSummary],
                options: null,
                cancellationToken).ConfigureAwait(false);

            ZVecRagTelemetry.RecordUsageDetails(ZVecRagConstants.TelemetryStageEmbed, summaryEmbedding.Usage);

            await summaryCollection.UpsertAsync(
                [
                    new ZVecRagSectionSummaryV1
                    {
                        SectionSummaryId = item.SectionSummaryId,
                        SourceDoc = documentId,
                        SourceUri = sourceUri,
                        SectionIndex = item.SectionIndex,
                        Summary = sanitizedSummary,
                        DenseVector = summaryEmbedding[0].Vector
                    }
                ],
                cancellationToken).ConfigureAwait(false);

            foreach (TextChunk childChunk in chunker.Chunk(item.SectionText))
            {
                cancellationToken.ThrowIfCancellationRequested();
                GeneratedEmbeddings<Embedding<float>> childEmbedding = await embedder.GenerateAsync(
                    [childChunk.Text],
                    options: null,
                    cancellationToken).ConfigureAwait(false);

                ZVecRagTelemetry.RecordUsageDetails(ZVecRagConstants.TelemetryStageEmbed, childEmbedding.Usage);

                string chunkId = ZVecChunkIdGenerator.Compute(sourceUri, chunker.StrategyId, nextChunkIndex);
                await chunkCollection.UpsertAsync(
                    [
                        new ZVecRagRecordV1
                        {
                            ChunkId = chunkId,
                            SourceDoc = documentId,
                            SourceUri = sourceUri,
                            SourceHash = sourceHash,
                            Page = options.Page ?? -1,
                            Offset = childChunk.Offset,
                            ChunkIndex = nextChunkIndex,
                            Text = childChunk.Text,
                            SectionSummaryId = item.SectionSummaryId,
                            DenseVector = childEmbedding[0].Vector
                        }
                    ],
                    cancellationToken).ConfigureAwait(false);

                chunkIds.Add(chunkId);
                nextChunkIndex++;
            }
        }

        return new IngestionResult(documentId, chunkIds.Count, chunkIds);
    }

    /// <summary>Deletes all section summaries for a document.</summary>
    public static async Task DeleteAllDocumentSummariesAsync(
        VectorStoreCollection<string, ZVecRagSectionSummaryV1> collection,
        string documentId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var keys = new List<string>();
            await foreach (var record in collection.GetAsync(
                r => r.SourceDoc == documentId,
                ZVecRagConstants.DuplicateScanBatchSize,
                SummaryScanOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                keys.Add(record.SectionSummaryId);
            }

            if (keys.Count == 0)
            {
                break;
            }

            await collection.DeleteAsync(keys, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<string> SummarizeSectionAsync(
        IChatClient chat,
        string sanitizedSectionText,
        int maxSummaryTokens,
        CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, ZVecRagConstants.SectionSummarySystemPolicy),
            new(ChatRole.User, sanitizedSectionText)
        };

        ChatResponse response = await chat.GetResponseAsync(
            messages,
            new ChatOptions { MaxOutputTokens = maxSummaryTokens },
            cancellationToken).ConfigureAwait(false);

        return response.Text ?? string.Empty;
    }

    private readonly record struct SectionWorkItem(int SectionIndex, string SectionText, string SectionSummaryId);
}
