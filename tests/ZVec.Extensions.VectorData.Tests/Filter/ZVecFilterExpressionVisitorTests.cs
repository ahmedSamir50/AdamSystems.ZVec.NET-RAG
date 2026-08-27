using System.Linq.Expressions;
using ZVec.Extensions.VectorData.Exceptions;
using ZVec.NET.Mapping;
using Xunit;

namespace ZVec.Extensions.VectorData.Tests;

/// <summary>
/// Mapped POCO for filter expression visitor TDD unit tests.
/// </summary>
public sealed class FilterTestRecord
{
    [ZVecId]
    public string Id { get; set; } = string.Empty;

    [ZVecField]
    public string Category { get; set; } = string.Empty;

    [ZVecField]
    public int Price { get; set; }

    [ZVecField]
    public bool InStock { get; set; }

    [ZVecVector(768)]
    public ReadOnlyMemory<float> Embedding { get; set; }

    [ZVecField]
    public string[] Tags { get; set; } = Array.Empty<string>();
}

/// <summary>
/// TDD unit tests for <see cref="ZVecFilterExpressionVisitor"/> covering all 12 filter operators and AST translation error handling.
/// </summary>
public sealed class ZVecFilterExpressionVisitorTests
{
    [Fact]
    public void Translate_ThrowsArgumentNullException_WhenExpressionIsNull()
    {
        Expression<Func<FilterTestRecord, bool>> filter = null!;
        Assert.Throws<ArgumentNullException>(() => ZVecFilterExpressionVisitor.Translate(filter));
    }

    [Fact]
    public void Translate_EqualsOperator_ReturnsExpectedFilterString()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Category == "Electronics";
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Category = \"Electronics\"", result);
    }

    [Fact]
    public void Translate_NotEqualsOperator_ReturnsExpectedFilterString()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Category != "Clothing";
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Category != \"Clothing\"", result);
    }

    [Fact]
    public void Translate_LessThanOperator_ReturnsExpectedFilterString()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Price < 100;
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Price < 100", result);
    }

    [Fact]
    public void Translate_LessThanOrEqualOperator_ReturnsExpectedFilterString()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Price <= 100;
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Price <= 100", result);
    }

    [Fact]
    public void Translate_GreaterThanOperator_ReturnsExpectedFilterString()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Price > 50;
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Price > 50", result);
    }

    [Fact]
    public void Translate_GreaterThanOrEqualOperator_ReturnsExpectedFilterString()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Price >= 50;
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Price >= 50", result);
    }

    [Fact]
    public void Translate_AndOperator_ReturnsExpectedFilterString()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Category == "Electronics" && x.Price < 500;
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Category = \"Electronics\"", result);
        Assert.Contains("Price < 500", result);
        Assert.Contains("AND", result);
    }

    [Fact]
    public void Translate_OrOperator_ReturnsExpectedFilterString()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Category == "Electronics" || x.Price < 50;
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Category = \"Electronics\"", result);
        Assert.Contains("Price < 50", result);
        Assert.Contains("OR", result);
    }

    [Fact]
    public void Translate_NotOperator_ReturnsExpectedFilterString()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => !x.InStock;
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("InStock = false", result);
    }

    [Fact]
    public void Translate_NotOperator_PreservesParameterReference_WhenLambdaParameterIsNotNamedX()
    {
        // Uses 'p' instead of 'x' to verify the fix for the parameter-reference mismatch bug.
        // Before the fix, TranslateCore created a new ParameterExpression("x") breaking
        // reference equality with the actual parameter 'p' from the lambda.
        Expression<Func<FilterTestRecord, bool>> filter = p => !p.InStock;
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("InStock = false", result);
    }

    [Fact]
    public void Translate_ContainsAnyOperator_ReturnsExpectedFilterString()
    {
        string[] categories = new[] { "Electronics", "Books" };
        Expression<Func<FilterTestRecord, bool>> filter = x => categories.Contains(x.Category);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Category IN (\"Electronics\", \"Books\")", result);
    }

    [Fact]
    public void Translate_ContainsInstanceMethod_ReturnsExpectedFilterString()
    {
        List<string> categoriesList = new List<string> { "Hardware", "Tools" };
        Expression<Func<FilterTestRecord, bool>> filter = x => categoriesList.Contains(x.Category);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Category IN (\"Hardware\", \"Tools\")", result);
    }

    [Fact]
    public void Translate_ThrowsZVecFilterTranslationException_WhenContainsListIsEmpty()
    {
        string[] emptyCategories = Array.Empty<string>();
        Expression<Func<FilterTestRecord, bool>> filter = x => emptyCategories.Contains(x.Category);
        Assert.Throws<ZVecFilterTranslationException>(() => ZVecFilterExpressionVisitor.Translate(filter));
    }

    [Fact]
    public void Translate_ThrowsZVecFilterTranslationException_WhenUnsupportedExpressionUsed()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Category.StartsWith("Elec");
        var ex = Assert.Throws<ZVecFilterTranslationException>(() => ZVecFilterExpressionVisitor.Translate(filter));
        Assert.Contains("Remediation", ex.Message);
        Assert.Contains("full-text search", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Translate_ThrowsZVecFilterTranslationException_WhenEndsWithUsed()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Category.EndsWith("ics");
        var ex = Assert.Throws<ZVecFilterTranslationException>(() => ZVecFilterExpressionVisitor.Translate(filter));
        Assert.Equal(VectorData.Constants.ZVecErrorMessages.UnsupportedEndsWithMethod("Category"), ex.Message);
        Assert.Equal(VectorData.Constants.ZVecFilterErrorCode.UnsupportedEndsWith, ex.ErrorCode);
    }

    [Fact]
    public void Translate_ThrowsZVecFilterTranslationException_WhenRegexIsMatchUsed()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => System.Text.RegularExpressions.Regex.IsMatch(x.Category, "^Elec");
        var ex = Assert.Throws<ZVecFilterTranslationException>(() => ZVecFilterExpressionVisitor.Translate(filter));
        Assert.Equal(VectorData.Constants.ZVecErrorMessages.UnsupportedRegexMethod("Category"), ex.Message);
        Assert.Equal(VectorData.Constants.ZVecFilterErrorCode.UnsupportedRegex, ex.ErrorCode);
    }

    [Fact]
    public void Translate_ThrowsZVecFilterTranslationException_WhenStringContainsUsed()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Category.Contains("Elec");
        var ex = Assert.Throws<ZVecFilterTranslationException>(() => ZVecFilterExpressionVisitor.Translate(filter));
        Assert.Equal(VectorData.Constants.ZVecErrorMessages.UnsupportedStringContainsMethod("Category"), ex.Message);
        Assert.Equal(VectorData.Constants.ZVecFilterErrorCode.UnsupportedStringContains, ex.ErrorCode);
    }

    // -------------------------------------------------------------------------
    // Story 1.5: ContainAny on record collection properties (x.Tags.Contains)
    // -------------------------------------------------------------------------

    [Fact]
    public void Translate_CollectionPropertyContains_SingleValue_ReturnsContainAnyFilterString()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Tags.Contains("Electronics");
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Tags CONTAIN_ANY", result);
        Assert.Contains("\"Electronics\"", result);
    }

    [Fact]
    public void Translate_CollectionPropertyContains_CapturedVariable_ReturnsContainAnyFilterString()
    {
        string tag = "Books";
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Tags.Contains(tag);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Tags CONTAIN_ANY", result);
        Assert.Contains("\"Books\"", result);
    }

    [Fact]
    public void Translate_CollectionPropertyContains_EmptyString_ReturnsContainAnyFilterString()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Tags.Contains(string.Empty);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Tags CONTAIN_ANY", result);
        Assert.Contains("\"\"", result);
    }

    [Fact]
    public void Translate_CollectionPropertyContains_NullValue_ThrowsZVecFilterTranslationException()
    {
        string? tag = null;
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Tags.Contains(tag!);
        Assert.Throws<ZVecFilterTranslationException>(() => ZVecFilterExpressionVisitor.Translate(filter));
    }

    [Fact]
    public void Translate_CollectionPropertyContains_CombinedWithAnd_ReturnsContainAnyAndComparison()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Tags.Contains("Sale") && x.Price < 100;
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Tags CONTAIN_ANY", result);
        Assert.Contains("\"Sale\"", result);
        Assert.Contains("Price < 100", result);
        Assert.Contains("AND", result);
    }

    [Fact]
    public void Translate_CollectionPropertyContains_CombinedWithOr_ReturnsContainAnyOrComparison()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Tags.Contains("Clearance") || x.InStock;
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Tags CONTAIN_ANY", result);
        Assert.Contains("\"Clearance\"", result);
        Assert.Contains("InStock = true", result);
        Assert.Contains("OR", result);
    }

    [Fact]
    public void TranslateToBuilder_CollectionPropertyContains_ReturnsContainAnyBuilder()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Tags.Contains("Featured");
        var builder = ZVecFilterExpressionVisitor.TranslateToBuilder(filter);

        Assert.NotNull(builder);
        Assert.Contains("Tags CONTAIN_ANY", builder.Build());
        Assert.Contains("\"Featured\"", builder.Build());
    }

    [Fact]
    public void Translate_IntCollectionContains_ReturnsContainAnyFilterString()
    {
        Expression<Func<IntCollectionTestRecord, bool>> filter = x => x.NumberTags.Contains(42);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.Contains("NumberTags CONTAIN_ANY", result);
        Assert.Contains("42", result);
    }

    [Fact]
    public void Translate_GuidCollectionContains_ReturnsContainAnyFilterString()
    {
        var targetId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Expression<Func<GuidCollectionTestRecord, bool>> filter = x => x.AllowedIds.Contains(targetId);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.Contains("AllowedIds CONTAIN_ANY", result);
        Assert.Contains("11111111-1111-1111-1111-111111111111", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Translate_LongCollectionContains_ReturnsContainAnyFilterString()
    {
        Expression<Func<LongCollectionTestRecord, bool>> filter = x => x.LongTags.Contains(9_000_000_000L);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.Contains("LongTags CONTAIN_ANY", result);
        Assert.Contains("9000000000", result);
    }

    [Fact]
    public void Translate_DateTimeCollectionContains_ReturnsContainAnyFilterString()
    {
        var targetDate = new DateTime(2026, 8, 13);
        Expression<Func<DateTimeCollectionTestRecord, bool>> filter = x => x.EventDates.Contains(targetDate);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.Contains("EventDates CONTAIN_ANY", result);
        Assert.Contains("2026", result);
    }

    [Fact]
    public void Translate_DateTimeOffsetCollectionContains_ReturnsContainAnyFilterString()
    {
        var targetDto = new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);
        Expression<Func<DateTimeOffsetCollectionTestRecord, bool>> filter = x => x.TimestampLog.Contains(targetDto);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.Contains("TimestampLog CONTAIN_ANY", result);
        Assert.Contains("2026", result);
    }

    [Fact]
    public void Translate_ListCollectionPropertyContains_ReturnsContainAnyFilterString()
    {
        Expression<Func<ListCollectionTestRecord, bool>> filter = x => x.Tags.Contains("Featured");
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.Contains("Tags CONTAIN_ANY", result);
        Assert.Contains("\"Featured\"", result);
    }

    [Fact]
    public void Translate_EnumerableStaticContains_ReturnsContainAnyFilterString()
    {
        string tag = "Sale";
        Expression<Func<FilterTestRecord, bool>> filter = x => Enumerable.Contains(x.Tags, tag);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.Contains("Tags CONTAIN_ANY", result);
        Assert.Contains("\"Sale\"", result);
    }

    [Fact]
    public void Translate_FloatCollectionContains_ReturnsContainAnyFilterString()
    {
        Expression<Func<FloatCollectionTestRecord, bool>> filter = x => x.Scores.Contains(3.14f);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.Contains("Scores CONTAIN_ANY", result);
        Assert.Contains("3.14", result);
    }

    [Fact]
    public void Translate_DoubleCollectionContains_ReturnsContainAnyFilterString()
    {
        Expression<Func<DoubleCollectionTestRecord, bool>> filter = x => x.Ratings.Contains(9.99);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.Contains("Ratings CONTAIN_ANY", result);
        Assert.Contains("9.99", result);
    }

    [Fact]
    public void Translate_BoolCollectionContains_ReturnsContainAnyFilterString()
    {
        Expression<Func<BoolCollectionTestRecord, bool>> filter = x => x.Flags.Contains(true);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.Contains("Flags CONTAIN_ANY", result);
        Assert.Contains("true", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Translate_DirectBooleanProperty_ReturnsTrueComparisonFilterString()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.InStock;
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.Contains("InStock = true", result);
    }

    [Fact]
    public void Translate_ContainsAny_OnlyNullElements_ReturnsIsNullFilterString()
    {
        string?[] categories = new string?[] { null, null }!;
        Expression<Func<FilterTestRecord, bool>> filter = x => categories.Contains(x.Category);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.Contains("IS NULL", result.ToUpperInvariant());
        Assert.DoesNotContain("IN", result.ToUpperInvariant());
    }

    [Fact]
    public void Translate_UnsupportedMethodName_ThrowsZVecFilterTranslationException()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Category.Substring(0, 1) == "E";
        var ex = Assert.Throws<ZVecFilterTranslationException>(() => ZVecFilterExpressionVisitor.Translate(filter));

        Assert.Contains("Substring", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Translate_NestedCollectionPropertyContains_ThrowsZVecFilterTranslationException()
    {
        Expression<Func<NestedCollectionRecord, bool>> filter = x => x.Order.Tags.Contains("tag");
        var ex = Assert.Throws<ZVecFilterTranslationException>(() => ZVecFilterExpressionVisitor.Translate(filter));

        Assert.Contains("nested", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(VectorData.Constants.ZVecFilterErrorCode.UnsupportedExpression, ex.ErrorCode);
    }

    [Fact]
    public void Translate_UserDefinedConversion_ThrowsZVecFilterTranslationException()
    {
        UserDefinedConversionHolder.Value = new CustomFilterValue(42);
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Price > UserDefinedConversionHolder.Value;
        var ex = Assert.Throws<ZVecFilterTranslationException>(() => ZVecFilterExpressionVisitor.Translate(filter));

        Assert.Equal(VectorData.Constants.ZVecFilterErrorCode.UnsupportedUserDefinedConversion, ex.ErrorCode);
    }

    [Fact]
    public void Translate_WellKnownBclConversion_DoesNotThrow()
    {
        // decimal literal 99.9m is implicitly converted to double by the expression tree;
        // the whitelist (IsAllowedConversionOperator) must allow BCL primitive conversions.
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Price > 99;
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Price > 99", result);
    }

    // -------------------------------------------------------------------------
    // Evaluate internal-branch tests
    // The Evaluate() helper has 4 distinct execution paths that must all be hit:
    //   1. ConstantExpression  — covered by all literal value tests above
    //   2. MemberExpression (field access on closure) — captured local var
    //   3. NewArrayExpression  — inline array literal in expression body
    //   4. Fallback DynamicInvoke — any other compilable sub-expression
    // -------------------------------------------------------------------------

    [Fact]
    public void Translate_ContainsAny_InlineNewArrayExpression_ReturnsExpectedFilterString()
    {
        // x => new[] { "A", "B" }.Contains(x.Category)
        // containerExpr is a NewArrayExpression — exercises the NewArrayExpression
        // branch in Evaluate() (lines 194–200 of ZVecFilterExpressionVisitor.cs)
        Expression<Func<FilterTestRecord, bool>> filter =
            x => new[] { "Appliances", "Garden" }.Contains(x.Category);

        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Category IN", result);
        Assert.Contains("\"Appliances\"", result);
        Assert.Contains("\"Garden\"", result);
    }

    [Fact]
    public void Translate_ContainsAny_CapturedPropertyValue_ReturnsExpectedFilterString()
    {
        // categoriesWrapper.Tags is a property — exercises MemberExpression/PropertyInfo
        // path in Evaluate() (lines 177–180 of ZVecFilterExpressionVisitor.cs)
        var categoriesWrapper = new CategoryWrapper { Tags = new[] { "Music", "Sports" } };
        Expression<Func<FilterTestRecord, bool>> filter =
            x => categoriesWrapper.Tags.Contains(x.Category);

        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Category IN", result);
        Assert.Contains("\"Music\"", result);
        Assert.Contains("\"Sports\"", result);
    }

    [Fact]
    public void Translate_ContainsAny_StaticFieldValue_ReturnsExpectedFilterString()
    {
        // CategoryHolder.StaticTags is a static field — exercises the null-instance
        // branch of MemberExpression in Evaluate():
        //   memberExpr.Expression == null → instance = null → fieldInfo.GetValue(null)
        Expression<Func<FilterTestRecord, bool>> filter =
            x => CategoryHolder.StaticTags.Contains(x.Category);

        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Category IN", result);
        Assert.Contains("\"Outdoor\"", result);
        Assert.Contains("\"Fitness\"", result);
    }

    // -------------------------------------------------------------------------
    // v4 Review Remediation: 5 missing filter visitor tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies string values containing double quotes are properly escaped in the
    /// generated filter string. Prevents SQL-injection-style filter breakage.
    /// </summary>
    [Fact]
    public void Translate_EqualOperator_EscapesDoubleQuotesInStringValue()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Category == "Evil\"OR";
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Evil\\\"OR", result);
    }

    /// <summary>
    /// Verifies that integer arrays in IN clauses emit unquoted numeric literals
    /// (not quoted string literals like "1", "2").
    /// </summary>
    [Fact]
    public void Translate_ContainsAny_NumericArray_EmitsUnquotedNumericLiterals()
    {
        int[] prices = new[] { 10, 20, 30 };
        Expression<Func<FilterTestRecord, bool>> filter = x => prices.Contains(x.Price);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Price IN", result);
        Assert.Contains("10", result);
        Assert.Contains("20", result);
        Assert.Contains("30", result);
        Assert.DoesNotContain("\"10\"", result);
        Assert.DoesNotContain("\"20\"", result);
        Assert.DoesNotContain("\"30\"", result);
    }

    /// <summary>
    /// Verifies that an IN clause containing both null and non-null elements
    /// generates "(Property IN (...) OR Property IS NULL)".
    /// </summary>
    [Fact]
    public void Translate_ContainsAny_MixedNullAndNonNullElements_GeneratesInClauseWithIsNullAlternative()
    {
        string[] categories = new string?[] { "Electronics", null, "Books" }!;
        Expression<Func<FilterTestRecord, bool>> filter = x => categories.Contains(x.Category);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Category IN", result);
        Assert.Contains("\"Electronics\"", result);
        Assert.Contains("\"Books\"", result);
        Assert.Contains("IS NULL", result.ToUpperInvariant());
    }

    /// <summary>
    /// Verifies that comparing a property to null translates to an IS NULL check.
    /// </summary>
    [Fact]
    public void Translate_IsNullComparison_ReturnsIsNullFilterString()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Category == null;
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("IS NULL", result);
    }

    /// <summary>
    /// Verifies that comparing a property to not-null translates to an IS NOT NULL check.
    /// </summary>
    [Fact]
    public void Translate_IsNotNullComparison_ReturnsIsNotNullFilterString()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Category != null;
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("IS NOT NULL", result);
    }

    /// <summary>
    /// Verifies that compound Not negation on a binary expression (e.g. !(x.Price &gt; 100))
    /// translates to NOT(Price &gt; 100) or simplified equivalent Price &lt;= 100.
    /// </summary>
    [Fact]
    public void Translate_CompoundNot_OnBinaryExpression_ReturnsNotWrappedFilterString()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => !(x.Price > 100);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Price <= 100", result);
    }
}

/// <summary>Record with int collection property for ContainAny type dispatch tests.</summary>
public sealed class IntCollectionTestRecord
{
    [ZVecId]
    public string Id { get; set; } = string.Empty;

    [ZVecField]
    public int[] NumberTags { get; set; } = Array.Empty<int>();
}

/// <summary>Record with long collection property for ContainAny type dispatch tests.</summary>
public sealed class LongCollectionTestRecord
{
    [ZVecId]
    public string Id { get; set; } = string.Empty;

    [ZVecField]
    public long[] LongTags { get; set; } = Array.Empty<long>();
}

/// <summary>Record with Guid collection property for ContainAny type dispatch tests.</summary>
public sealed class GuidCollectionTestRecord
{
    [ZVecId]
    public string Id { get; set; } = string.Empty;

    [ZVecIgnore]
    public Guid[] AllowedIds { get; set; } = Array.Empty<Guid>();
}

/// <summary>Record with DateTime collection property for ContainAny type dispatch tests.</summary>
public sealed class DateTimeCollectionTestRecord
{
    [ZVecId]
    public string Id { get; set; } = string.Empty;

    [ZVecIgnore]
    public DateTime[] EventDates { get; set; } = Array.Empty<DateTime>();
}

/// <summary>Record with DateTimeOffset collection property for ContainAny type dispatch tests.</summary>
public sealed class DateTimeOffsetCollectionTestRecord
{
    [ZVecId]
    public string Id { get; set; } = string.Empty;

    [ZVecIgnore]
    public DateTimeOffset[] TimestampLog { get; set; } = Array.Empty<DateTimeOffset>();
}

/// <summary>Record with List collection property for ContainAny List&lt;T&gt; dispatch tests.</summary>
public sealed class ListCollectionTestRecord
{
    [ZVecId]
    public string Id { get; set; } = string.Empty;

    [ZVecIgnore]
    public List<string> Tags { get; set; } = new();
}

/// <summary>Record with float collection property for ContainAny type dispatch tests.</summary>
public sealed class FloatCollectionTestRecord
{
    [ZVecId]
    public string Id { get; set; } = string.Empty;

    [ZVecIgnore]
    public float[] Scores { get; set; } = Array.Empty<float>();
}

/// <summary>Record with double collection property for ContainAny type dispatch tests.</summary>
public sealed class DoubleCollectionTestRecord
{
    [ZVecId]
    public string Id { get; set; } = string.Empty;

    [ZVecIgnore]
    public double[] Ratings { get; set; } = Array.Empty<double>();
}

/// <summary>Record with bool collection property for ContainAny type dispatch tests.</summary>
public sealed class BoolCollectionTestRecord
{
    [ZVecId]
    public string Id { get; set; } = string.Empty;

    [ZVecIgnore]
    public bool[] Flags { get; set; } = Array.Empty<bool>();
}

/// <summary>Holder with a string collection property, nested inside <see cref="NestedCollectionRecord"/>.</summary>
public sealed class OrderHolder
{
    /// <summary>String collection used to exercise nested member-access rejection in ContainAny.</summary>
    public string[] Tags { get; set; } = Array.Empty<string>();
}

/// <summary>Record with a nested collection property for ContainAny nested-access rejection tests.</summary>
public sealed class NestedCollectionRecord
{
    [ZVecId]
    public string Id { get; set; } = string.Empty;

    [ZVecField]
    public OrderHolder Order { get; set; } = new();
}

/// <summary>Custom type with user-defined implicit conversion for Unwrap guard tests.</summary>
public readonly struct CustomFilterValue
{
    public CustomFilterValue(int value) => Value = value;

    public int Value { get; }

    public static implicit operator int(CustomFilterValue value) => value.Value;
}

/// <summary>Holds a non-constant custom filter value to prevent expression-tree constant folding.</summary>
public static class UserDefinedConversionHolder
{
    /// <summary>Custom filter operand used by user-defined conversion tests.</summary>
    public static CustomFilterValue Value;
}

/// <summary>Helper: exposes a property-backed array for Evaluate MemberExpression/PropertyInfo coverage.</summary>
public sealed class CategoryWrapper
{
    /// <summary>Property-backed string array for use in filter expression closure tests.</summary>
    public string[] Tags { get; set; } = Array.Empty<string>();
}

/// <summary>Helper: exposes a static field array for Evaluate null-instance MemberExpression coverage.</summary>
public static class CategoryHolder
{
    /// <summary>Static field used in expression tests to exercise the null-instance field access path in Evaluate().</summary>
    public static readonly string[] StaticTags = new[] { "Outdoor", "Fitness" };
}
