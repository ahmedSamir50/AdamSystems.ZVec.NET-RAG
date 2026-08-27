namespace ZVec.Rag.Models;

/// <summary>
/// Optional parameters for document ingestion (Story 2.2 expands duplicate handling).
/// </summary>
public sealed class IngestOptions
{
    /// <summary>Gets or sets the source URI recorded on chunk metadata.</summary>
    public string? SourceUri { get; set; }

    /// <summary>Gets or sets an optional 1-based page number.</summary>
    public int? Page { get; set; }
}
