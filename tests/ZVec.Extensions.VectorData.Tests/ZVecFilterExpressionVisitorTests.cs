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
        Assert.Throws<ZVecFilterTranslationException>(() => ZVecFilterExpressionVisitor.Translate(filter));
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
