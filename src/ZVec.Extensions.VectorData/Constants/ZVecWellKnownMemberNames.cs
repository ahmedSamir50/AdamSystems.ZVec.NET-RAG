namespace ZVec.Extensions.VectorData.Constants;

/// <summary>
/// Well-known CLR and LINQ member names used during filter expression translation.
/// </summary>
public static class ZVecWellKnownMemberNames
{
    /// <summary>Implicit conversion operator name.</summary>
    public const string OpImplicit = "op_Implicit";

    /// <summary>Explicit conversion operator name.</summary>
    public const string OpExplicit = "op_Explicit";

    /// <summary><see cref="string.StartsWith(string)"/> method name.</summary>
    public const string StartsWith = "StartsWith";

    /// <summary><see cref="string.EndsWith(string)"/> method name.</summary>
    public const string EndsWith = "EndsWith";

    /// <summary><see cref="System.Text.RegularExpressions.Regex.IsMatch(string)"/> method name.</summary>
    public const string IsMatch = "IsMatch";

    /// <summary>Collection or string <c>Contains</c> method name.</summary>
    public const string Contains = "Contains";

    /// <summary>Fallback member name when property resolution fails.</summary>
    public const string UnknownMember = "unknown";

    /// <summary>Prefix for <see cref="ReadOnlySpan{T}"/> type names in conversion checks.</summary>
    public const string ReadOnlySpanTypeNamePrefix = "ReadOnlySpan";
}
