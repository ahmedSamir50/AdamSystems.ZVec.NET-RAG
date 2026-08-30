using Microsoft.Extensions.VectorData;
using ZVec.NET.Mapping;
using ZVec.Rag.ONNX;

namespace ZVec.Rag.ONNX.Schema;

/// <summary>
/// Multimodal RAG chunk record (CLIP / text+image) for future Sample 05.
/// </summary>
public sealed class ZVecRagMultimodalRecordV1
{
    /// <summary>Content-addressable chunk identifier.</summary>
    [VectorStoreKey]
    [ZVecId]
    public string ChunkId { get; set; } = string.Empty;

    /// <summary>Stable document identifier.</summary>
    [VectorStoreData(IsIndexed = true)]
    [ZVecField]
    public string SourceDoc { get; set; } = string.Empty;

    /// <summary>Display URI, file path, or URL.</summary>
    [VectorStoreData]
    [ZVecField]
    public string SourceUri { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of source content.</summary>
    [VectorStoreData(IsIndexed = true)]
    [ZVecField]
    public string SourceHash { get; set; } = string.Empty;

    /// <summary>Page number when applicable; -1 for N/A.</summary>
    [VectorStoreData(IsIndexed = true)]
    [ZVecField]
    public int Page { get; set; } = -1;

    /// <summary>Character offset in extracted text.</summary>
    [VectorStoreData]
    [ZVecField]
    public long Offset { get; set; }

    /// <summary>0-based chunk sequence number.</summary>
    [VectorStoreData]
    [ZVecField]
    public int ChunkIndex { get; set; }

    /// <summary>Chunk text or image caption payload.</summary>
    [VectorStoreData(IsFullTextIndexed = true)]
    [ZVecField]
    public string Text { get; set; } = string.Empty;

    /// <summary>Modality discriminator: <see cref="OnnxConstants.SourceKindText"/> or <see cref="OnnxConstants.SourceKindImage"/>.</summary>
    [VectorStoreData(IsIndexed = true)]
    [ZVecField]
    public string SourceKind { get; set; } = OnnxConstants.SourceKindText;

    /// <summary>Dense embedding vector (512-d for CLIP).</summary>
    [VectorStoreVector(OnnxConstants.ClipDimensions)]
    [ZVecVector(OnnxConstants.ClipDimensions)]
    public ReadOnlyMemory<float> DenseVector { get; set; }
}
