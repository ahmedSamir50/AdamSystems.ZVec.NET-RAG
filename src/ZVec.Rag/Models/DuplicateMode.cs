namespace ZVec.Rag.Models;

/// <summary>
/// Controls behavior when ingesting a document that already exists in the index.
/// </summary>
public enum DuplicateMode
{
    /// <summary>Delete existing chunks for <c>SourceDoc</c> before inserting new chunks.</summary>
    Replace = 0,

    /// <summary>Append new chunks after the highest existing <c>ChunkIndex</c> for the document.</summary>
    Append = 1,

    /// <summary>Skip ingestion when any chunk exists for the document.</summary>
    Skip = 2
}
