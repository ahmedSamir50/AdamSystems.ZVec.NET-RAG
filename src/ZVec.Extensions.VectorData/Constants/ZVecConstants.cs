namespace ZVec.Extensions.VectorData.Constants;

/// <summary>
/// Container for solution-wide core constants (Zero Magic Strings).
/// </summary>
public static class ZVecConstants
{
    /// <summary>Default maximum result limit for vector queries if unspecified.</summary>
    public const int DefaultQueryLimit = 10;

    /// <summary>
    /// Fallback similarity score threshold floor (0.0 to 1.0).
    /// </summary>
    public const float DefaultMinScoreThreshold = 0.0f;

    /// <summary>
    /// Fallback vector dimension used when a collection's vector dimension cannot be
    /// resolved from the type model (e.g. dynamic collections). Matches the most common
    /// embedding model dimension (nomic-embed-text / bge-small) used in samples.
    /// </summary>
    public const int DefaultVectorDimension = 768;

    /// <summary>
    /// Default Reciprocal Rank Fusion (RRF) smoothing constant <c>k</c>.
    /// Standard RRF value used by the native <c>ZVecRrfReranker</c>.
    /// </summary>
    public const int DefaultRrfRankConstant = 60;

    /// <summary>
    /// Fallback FTS field storage name when no indexed text property can be resolved.
    /// </summary>
    public const string DefaultFullTextFieldName = "Content";

    /// <summary>
    /// Fallback dense vector field storage name when schema metadata is unavailable.
    /// </summary>
    public const string DefaultVectorFieldName = "Vector";
}
