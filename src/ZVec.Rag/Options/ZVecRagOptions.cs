using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ZVec.Extensions.VectorData.Store;
using ZVec.Rag.Models;

namespace ZVec.Rag.Options;

/// <summary>
/// Top-level configuration for <c>AddZVecRag</c> registration.
/// </summary>
public sealed class ZVecRagOptions
{
    /// <summary>Gets or sets the storage directory for native ZVec collections.</summary>
    public string StoragePath { get; set; } = "./rag.zvec";

    /// <summary>Gets or sets the embedder used for ingestion and query encoding.</summary>
    public IEmbeddingGenerator<string, Embedding<float>>? Embedder { get; set; }

    /// <summary>Gets or sets the chat client used for answer generation.</summary>
    public IChatClient? Chat { get; set; }

    /// <summary>Gets or sets the RRF smoothing constant for hybrid search.</summary>
    public int RrfK { get; set; } = Constants.ZVecRagConstants.DefaultRrfK;

    /// <summary>Gets or sets the maximum context token budget for retrieved chunks.</summary>
    public int MaxContextTokens { get; set; } = Constants.ZVecRagConstants.DefaultMaxContextTokens;

    /// <summary>Gets or sets tokens reserved for LLM generation output.</summary>
    public int GenerationReserveTokens { get; set; } = Constants.ZVecRagConstants.DefaultGenerationReserveTokens;

    /// <summary>Gets or sets prompt context packing strategy.</summary>
    public ContextPackingStrategy ContextPacking { get; set; } = ContextPackingStrategy.ScoreDescending;

    /// <summary>Gets or sets citation list ordering for UI responses.</summary>
    public CitationOrder CitationOrder { get; set; } = CitationOrder.ScoreDescending;

    /// <summary>Gets or sets hybrid retrieval top-k.</summary>
    public int RetrieveTopK { get; set; } = Constants.ZVecRagConstants.DefaultRetrieveTopK;

    /// <summary>Gets or sets nested vector store connector options.</summary>
    public ZVecVectorStoreOptions VectorStore { get; set; } = new();

    /// <summary>Gets or sets logging verbosity for RAG pipeline components.</summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Warning;

    /// <summary>Gets or sets the native collection name for RAG chunks.</summary>
    public string CollectionName { get; set; } = Constants.ZVecRagConstants.DefaultCollectionName;

    /// <summary>Gets or sets optional Tiktoken encoding override (e.g. <c>cl100k_base</c>, <c>o200k_base</c>).</summary>
    public string? TokenizerEncoding { get; set; }

    /// <summary>Gets or sets optional SentencePiece/WordPiece model path loaded via <see cref="FileStream"/>.</summary>
    public string? TokenizerModelPath { get; set; }
}
