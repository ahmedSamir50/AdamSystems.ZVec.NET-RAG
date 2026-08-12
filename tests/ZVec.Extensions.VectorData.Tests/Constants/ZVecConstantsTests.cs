using ZVec.Extensions.VectorData.Constants;
using ZVec.Extensions.VectorData.Exceptions;
using Xunit;

namespace ZVec.Extensions.VectorData.Tests.Constants;

/// <summary>
/// TDD unit test suite verifying zero magic strings, error message formatting, enum bounds, and custom exceptions.
/// </summary>
public sealed class ZVecConstantsTests
{
    [Fact]
    public void ZVecFilterOperators_ContainsAllExpectedEnumMembers()
    {
        var values = Enum.GetValues<ZVecFilterOperators>();

        Assert.Contains(ZVecFilterOperators.Equals, values);
        Assert.Contains(ZVecFilterOperators.NotEquals, values);
        Assert.Contains(ZVecFilterOperators.LessThan, values);
        Assert.Contains(ZVecFilterOperators.LessThanOrEqual, values);
        Assert.Contains(ZVecFilterOperators.GreaterThan, values);
        Assert.Contains(ZVecFilterOperators.GreaterThanOrEqual, values);
        Assert.Contains(ZVecFilterOperators.And, values);
        Assert.Contains(ZVecFilterOperators.Or, values);
        Assert.Contains(ZVecFilterOperators.Not, values);
        Assert.Contains(ZVecFilterOperators.ContainsAny, values);
        Assert.Contains(ZVecFilterOperators.IsNull, values);
        Assert.Contains(ZVecFilterOperators.IsNotNull, values);
        Assert.Equal(12, values.Length);
    }

    [Theory]
    [InlineData("test_collection", "Collection 'test_collection' does not exist.")]
    [InlineData("my_vectors", "Collection 'my_vectors' does not exist.")]
    public void ZVecErrorMessages_FormatsCollectionNotFoundMessage(string collectionName, string expectedMessage)
    {
        string actual = ZVecErrorMessages.CollectionNotFound(collectionName);
        Assert.Equal(expectedMessage, actual);
    }

    [Fact]
    public void ZVecErrorMessages_ThrowsArgumentException_WhenCollectionNameNullOrEmpty()
    {
        Assert.Throws<ArgumentException>(() => ZVecErrorMessages.CollectionNotFound(null!));
        Assert.Throws<ArgumentException>(() => ZVecErrorMessages.CollectionNotFound(string.Empty));
        Assert.Throws<ArgumentException>(() => ZVecErrorMessages.CollectionNotFound("   "));
    }

    [Theory]
    [InlineData("UnsupportedExpression", "Expression 'UnsupportedExpression' cannot be translated to ZVecFilterBuilder.")]
    public void ZVecErrorMessages_FormatsUnsupportedFilterMessage(string exprText, string expectedMessage)
    {
        string actual = ZVecErrorMessages.UnsupportedFilterExpression(exprText);
        Assert.Equal(expectedMessage, actual);
    }

    [Fact]
    public void ZVecErrorMessages_ThrowsArgumentException_WhenExpressionTextNullOrEmpty()
    {
        var exNull = Assert.Throws<ArgumentException>(() => ZVecErrorMessages.UnsupportedFilterExpression(null!));
        Assert.Contains(ZVecErrorMessages.NullOrEmptyExpressionText, exNull.Message);

        var exEmpty = Assert.Throws<ArgumentException>(() => ZVecErrorMessages.UnsupportedFilterExpression(string.Empty));
        Assert.Contains(ZVecErrorMessages.NullOrEmptyExpressionText, exEmpty.Message);

        var exWhitespace = Assert.Throws<ArgumentException>(() => ZVecErrorMessages.UnsupportedFilterExpression("   "));
        Assert.Contains(ZVecErrorMessages.NullOrEmptyExpressionText, exWhitespace.Message);
    }

    [Fact]
    public void ZVecErrorMessages_ProvidesUnsupportedFilterMethodRemediationMessages()
    {
        Assert.Contains("Remediation", ZVecErrorMessages.UnsupportedStartsWithMethod());
        Assert.Contains("Remediation", ZVecErrorMessages.UnsupportedEndsWithMethod());
        Assert.Contains("Remediation", ZVecErrorMessages.UnsupportedRegexMethod());
        Assert.Contains("ContainAny", ZVecErrorMessages.UnsupportedStringContainsMethod());
    }

    [Fact]
    public void ZVecVectorDataException_ConstructsWithParamsAndInnerException()
    {
        var inner = new InvalidOperationException("Inner error");
        var ex = new ZVecVectorDataException("Test error message", inner);

        Assert.Equal("Test error message", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void ZVecFilterTranslationException_ConstructsWithParamsAndInnerException()
    {
        var inner = new NotSupportedException("Ast translation error");
        var ex = new ZVecFilterTranslationException("Filter error", inner);

        Assert.Equal("Filter error", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }
}
