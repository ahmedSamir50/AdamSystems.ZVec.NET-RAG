using Microsoft.Extensions.VectorData;
using ZVec.NET.Mapping;

namespace ZVec.Rag.Schema;

/// <summary>
/// Canonical RAG chunk record schema (v1) stored in the native ZVec collection.
/// </summary>
public sealed class ZVecRagRecordV1
{
    /// <summary>Content-addressable chunk identifier (SHA-256 hex).</summary>
    [VectorStoreKey]
    [ZVecId]
    public string ChunkId { get; set; } = string.Empty;

    /// <summary>Stable document identifier.</summary>
    [VectorStoreData(IsIndexed = true)]
    [ZVecField]
    public string SourceDoc { get; set; } = string.Empty;

    /// <summary>Display URI, file path, or URL for the source document.</summary>
    [VectorStoreData]
    [ZVecField]
    public string SourceUri { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of source content for deduplication.</summary>
    [VectorStoreData(IsIndexed = true)]
    [ZVecField]
    public string SourceHash { get; set; } = string.Empty;

    /// <summary>Page number when applicable; -1 for plain text/markdown (maps to null in citations).</summary>
    [VectorStoreData(IsIndexed = true)]
    [ZVecField]
    public int Page { get; set; } = -1;

    /// <summary>Character offset of chunk text in extracted document text.</summary>
    [VectorStoreData]
    [ZVecField]
    public long Offset { get; set; }

    /// <summary>0-based chunk sequence number within the source document.</summary>
    [VectorStoreData]
    [ZVecField]
    public int ChunkIndex { get; set; }

    /// <summary>Chunk text payload (full-text indexed for hybrid search).</summary>
    [VectorStoreData(IsFullTextIndexed = true)]
    [ZVecField]
    public string Text { get; set; } = string.Empty;

    /// <summary>Dense embedding vector.</summary>
    [VectorStoreVector(ZVecRagRecordV1.DefaultDimensions)]
    [ZVecVector(ZVecRagRecordV1.DefaultDimensions)]
    public ReadOnlyMemory<float> DenseVector { get; set; }

    /// <summary>Default embedding dimension for RAG collections.</summary>
    public const int DefaultDimensions = Constants.ZVecRagConstants.DefaultVectorDimensions;
}
