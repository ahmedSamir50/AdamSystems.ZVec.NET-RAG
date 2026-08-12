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
/// TDD Unit tests for ZVecFilterExpressionVisitor covering all 10 filter operators and AST translation error handling.
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
        Assert.Equal(ZVec.Extensions.VectorData.Constants.ZVecErrorMessages.UnsupportedEndsWithMethod(), ex.Message);
    }

    [Fact]
    public void Translate_ThrowsZVecFilterTranslationException_WhenRegexIsMatchUsed()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => System.Text.RegularExpressions.Regex.IsMatch(x.Category, "^Elec");
        var ex = Assert.Throws<ZVecFilterTranslationException>(() => ZVecFilterExpressionVisitor.Translate(filter));
        Assert.Equal(ZVec.Extensions.VectorData.Constants.ZVecErrorMessages.UnsupportedRegexMethod(), ex.Message);
    }

    [Fact]
    public void Translate_ThrowsZVecFilterTranslationException_WhenStringContainsUsed()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Category.Contains("Elec");
        var ex = Assert.Throws<ZVecFilterTranslationException>(() => ZVecFilterExpressionVisitor.Translate(filter));
        Assert.Equal(ZVec.Extensions.VectorData.Constants.ZVecErrorMessages.UnsupportedStringContainsMethod(), ex.Message);
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
    public void Translate_IsNullComparison_ReturnsIsNotNullFilterString()
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
