using ZVec.Extensions.VectorData.Constants;
using Xunit;

namespace ZVec.Extensions.VectorData.Tests.Constants;

/// <summary>
/// Verifies well-known CLR/LINQ member name constants match expected runtime tokens.
/// </summary>
public sealed class ZVecWellKnownMemberNamesTests
{
    [Fact]
    public void ConversionOperatorNames_MatchClrTokens()
    {
        Assert.Equal("op_Implicit", ZVecWellKnownMemberNames.OpImplicit);
        Assert.Equal("op_Explicit", ZVecWellKnownMemberNames.OpExplicit);
    }

    [Fact]
    public void FilterMethodNames_MatchClrTokens()
    {
        Assert.Equal("StartsWith", ZVecWellKnownMemberNames.StartsWith);
        Assert.Equal("EndsWith", ZVecWellKnownMemberNames.EndsWith);
        Assert.Equal("IsMatch", ZVecWellKnownMemberNames.IsMatch);
        Assert.Equal("Contains", ZVecWellKnownMemberNames.Contains);
    }

    [Fact]
    public void DirectoryExclusions_ContainExpectedInfrastructureFolders()
    {
        Assert.Contains(ZVecDirectoryNames.Bin, ZVecDirectoryNames.CollectionEnumerationExclusions);
        Assert.Contains(ZVecDirectoryNames.Git, ZVecDirectoryNames.CollectionEnumerationExclusions);
    }
}
