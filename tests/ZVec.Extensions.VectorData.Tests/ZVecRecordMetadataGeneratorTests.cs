using System.Reflection;
using Microsoft.Extensions.VectorData;
using ZVec.NET.Mapping;
using Xunit;

namespace ZVec.Extensions.VectorData.Tests;

/// <summary>
/// Sample record type for Roslyn Source Generator compile-time verification tests.
/// </summary>
public sealed class SampleGeneratorRecord
{
    /// <summary>Document Key.</summary>
    [ZVecId]
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>Title Field.</summary>
    [ZVecField]
    [VectorStoreData]
    public string Title { get; set; } = string.Empty;

    /// <summary>Vector Field.</summary>
    [ZVecVector(768)]
    [VectorStoreVector(768)]
    public ReadOnlyMemory<float> Vector { get; set; }
}

/// <summary>
/// TDD Unit tests verifying that the record POCO with both ZVec and VectorStore attributes
/// correctly exposes all annotated properties with the expected attribute metadata.
/// This complements the Roslyn SG tests in SourceGenerator.Tests that validate generator output.
/// </summary>
public sealed class SampleGeneratorRecordAttributeTests
{
    [Fact]
    public void SampleGeneratorRecord_HasAllExpectedVectorStoreAttributes()
    {
        var recordType = typeof(SampleGeneratorRecord);
        var properties = recordType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Equal(3, properties.Length);

        var idProp = Array.Find(properties, p => p.Name == nameof(SampleGeneratorRecord.Id));
        Assert.NotNull(idProp);
        Assert.True(Attribute.IsDefined(idProp, typeof(VectorStoreKeyAttribute)),
            "Id property must have [VectorStoreKey] attribute.");
        Assert.True(Attribute.IsDefined(idProp, typeof(ZVecIdAttribute)),
            "Id property must have [ZVecId] attribute.");

        var titleProp = Array.Find(properties, p => p.Name == nameof(SampleGeneratorRecord.Title));
        Assert.NotNull(titleProp);
        Assert.True(Attribute.IsDefined(titleProp, typeof(VectorStoreDataAttribute)),
            "Title property must have [VectorStoreData] attribute.");
        Assert.True(Attribute.IsDefined(titleProp, typeof(ZVecFieldAttribute)),
            "Title property must have [ZVecField] attribute.");

        var vectorProp = Array.Find(properties, p => p.Name == nameof(SampleGeneratorRecord.Vector));
        Assert.NotNull(vectorProp);
        Assert.True(Attribute.IsDefined(vectorProp, typeof(VectorStoreVectorAttribute)),
            "Vector property must have [VectorStoreVector] attribute.");
        var vectorAttr = vectorProp.GetCustomAttribute<VectorStoreVectorAttribute>();
        Assert.NotNull(vectorAttr);
        Assert.Equal(768, vectorAttr.Dimensions);
    }
}
