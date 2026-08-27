namespace ZVec.Rag.Models;

/// <summary>
/// Optional parameters for document ingestion.
/// </summary>
public sealed class IngestOptions
{
    /// <summary>Gets or sets the source URI recorded on chunk metadata.</summary>
    public string? SourceUri { get; set; }

    /// <summary>Gets or sets an optional 1-based page number.</summary>
    public int? Page { get; set; }

    /// <summary>Gets or sets duplicate handling mode (default <see cref="DuplicateMode.Replace"/>).</summary>
    public DuplicateMode OnDuplicate { get; set; } = DuplicateMode.Replace;

    /// <summary>Gets or sets an optional chunker override for this ingest operation.</summary>
    public Abstractions.IZVecTextChunker? Chunker { get; set; }
}
