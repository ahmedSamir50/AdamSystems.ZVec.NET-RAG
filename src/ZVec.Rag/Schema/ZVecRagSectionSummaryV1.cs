using Microsoft.Extensions.VectorData;
using ZVec.NET.Mapping;

namespace ZVec.Rag.Schema;

/// <summary>
/// Section-summary record schema (v1) stored in the <c>rag_section_summaries</c> collection.
/// </summary>
public sealed class ZVecRagSectionSummaryV1
{
    /// <summary>Content-addressable section-summary identifier (SHA-256 hex).</summary>
    [VectorStoreKey]
    [ZVecId]
    public string SectionSummaryId { get; set; } = string.Empty;

    /// <summary>Stable document identifier.</summary>
    [VectorStoreData(IsIndexed = true)]
    [ZVecField]
    public string SourceDoc { get; set; } = string.Empty;

    /// <summary>Display URI, file path, or URL for the source document.</summary>
    [VectorStoreData]
    [ZVecField]
    public string SourceUri { get; set; } = string.Empty;

    /// <summary>0-based section index within the source document.</summary>
    [VectorStoreData]
    [ZVecField]
    public int SectionIndex { get; set; }

    /// <summary>LLM-generated section summary (full-text indexed for hybrid search).</summary>
    [VectorStoreData(IsFullTextIndexed = true)]
    [ZVecField]
    public string Summary { get; set; } = string.Empty;

    /// <summary>Dense embedding vector of <see cref="Summary"/>.</summary>
    [VectorStoreVector(ZVecRagRecordV1.DefaultDimensions)]
    [ZVecVector(ZVecRagRecordV1.DefaultDimensions)]
    public ReadOnlyMemory<float> DenseVector { get; set; }
}
