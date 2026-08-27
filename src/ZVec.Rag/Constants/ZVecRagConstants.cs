namespace ZVec.Rag.Constants;

/// <summary>
/// Centralized constants for the ZVec.Rag pipeline.
/// </summary>
public static class ZVecRagConstants
{
    /// <summary>Default native collection name for RAG chunk storage.</summary>
    public const string DefaultCollectionName = "rag_chunks";

    /// <summary>Story 2.1 whole-text ingest strategy identifier for chunk id hashing.</summary>
    public const string WholeTextStrategyId = "whole-text-v1";

    /// <summary>Token-boundary chunking strategy identifier.</summary>
    public const string TokenChunkerStrategyId = "token-v1";

    /// <summary>Markdown heading-aware chunking strategy identifier.</summary>
    public const string MarkdownHeadingChunkerStrategyId = "markdown-heading-v1";

    /// <summary>Sentence-boundary chunking strategy identifier.</summary>
    public const string SentenceChunkerStrategyId = "sentence-v1";

    /// <summary>Default maximum tokens per chunk for <see cref="Ingestion.TokenTextChunker"/>.</summary>
    public const int DefaultChunkMaxTokens = 512;

    /// <summary>Default token overlap between consecutive token chunks.</summary>
    public const int DefaultChunkOverlapTokens = 64;

    /// <summary>Bounded channel capacity for parse stage.</summary>
    public const int ParseChannelCapacity = 1024;

    /// <summary>Bounded channel capacity for deduplication stage.</summary>
    public const int DedupChannelCapacity = 2048;

    /// <summary>Embedding batch size during ingestion.</summary>
    public const int EmbedBatchSize = 32;

    /// <summary>Vector upsert batch size during ingestion.</summary>
    public const int UpsertBatchSize = 100;

    /// <summary>Default maximum context tokens for <see cref="Generation.ContextPacker"/>.</summary>
    public const int DefaultMaxContextTokens = 4096;

    /// <summary>Default token reserve for LLM generation output.</summary>
    public const int DefaultGenerationReserveTokens = 512;

    /// <summary>Default reciprocal rank fusion smoothing constant.</summary>
    public const int DefaultRrfK = 60;

    /// <summary>Default hybrid retrieval top-k.</summary>
    public const int DefaultRetrieveTopK = 5;

    /// <summary>Batch size when scanning existing document chunks for duplicate handling.</summary>
    public const int DuplicateScanBatchSize = 10;

    /// <summary>Default dense vector dimension for <see cref="Schema.ZVecRagRecordV1"/>.</summary>
    public const int DefaultVectorDimensions = 768;

    /// <summary>UTF-8 plain text content type.</summary>
    public const string PlainTextContentType = "text/plain";

    /// <summary>Markdown content type.</summary>
    public const string MarkdownContentType = "text/markdown";

    /// <summary>Tiktoken encoding name for cl100k_base (used via CreateForEncoding).</summary>
    public const string Cl100kBaseEncoding = "cl100k_base";

    /// <summary>XML wrapper opening tag for retrieved context in prompts.</summary>
    public const string RetrievedContextOpenTag = "<retrieved_context>";

    /// <summary>XML wrapper closing tag for retrieved context in prompts.</summary>
    public const string RetrievedContextCloseTag = "</retrieved_context>";
}
