namespace ZVec.Extensions.VectorData.Attributes;

/// <summary>
/// Specifies that a text property in a POCO should be indexed for Full-Text Search (FTS)
/// in ZVec hybrid search queries.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class ZVecFullTextSearchAttribute : Attribute
{
    /// <summary>
    /// Gets or sets a value indicating whether full-text search indexing is enabled.
    /// </summary>
    public bool IsFullTextIndexed { get; set; } = true;
}
