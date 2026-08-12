namespace ZVec.Extensions.VectorData.Constants;

/// <summary>
/// Container for solution-wide core constants (Zero Magic Strings).
/// </summary>
public static class ZVecConstants
{
    /// <summary>Default maximum result limit for vector queries if unspecified.</summary>
    public const int DefaultQueryLimit = 10;

    /// <summary>Default similarity score threshold floor (0.0 to 1.0).</summary>
    public const float DefaultMinScoreThreshold = 0.0f;
}
