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

    /// <summary>Default maximum context tokens for <see cref="Generation.ContextPacker"/>.</summary>
    public const int DefaultMaxContextTokens = 4096;

    /// <summary>Default token reserve for LLM generation output.</summary>
    public const int DefaultGenerationReserveTokens = 512;

    /// <summary>Default reciprocal rank fusion smoothing constant.</summary>
    public const int DefaultRrfK = 60;

    /// <summary>Default hybrid retrieval top-k.</summary>
    public const int DefaultRetrieveTopK = 5;

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
