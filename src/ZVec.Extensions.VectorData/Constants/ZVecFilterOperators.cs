namespace ZVec.Extensions.VectorData.Constants;

/// <summary>
/// Supported filter expression comparison and logical operators for AST translation.
/// </summary>
public enum ZVecFilterOperators
{
    /// <summary>Equality operator (== / Equals).</summary>
    Equals = 1,

    /// <summary>Inequality operator (!= / Not Equals).</summary>
    NotEquals = 2,

    /// <summary>Less than operator (&lt;).</summary>
    LessThan = 3,

    /// <summary>Less than or equal operator (&lt;=).</summary>
    LessThanOrEqual = 4,

    /// <summary>Greater than operator (&gt;).</summary>
    GreaterThan = 5,

    /// <summary>Greater than or equal operator (&gt;=).</summary>
    GreaterThanOrEqual = 6,

    /// <summary>Logical AND operator (&amp;&amp;).</summary>
    And = 7,

    /// <summary>Logical OR operator (||).</summary>
    Or = 8,

    /// <summary>Logical NOT operator (!).</summary>
    Not = 9,

    /// <summary>Collection containment operator (ContainsAny).</summary>
    ContainsAny = 10,

    /// <summary>Null check operator (IsNull).</summary>
    IsNull = 11,

    /// <summary>Non-null check operator (IsNotNull).</summary>
    IsNotNull = 12
}
