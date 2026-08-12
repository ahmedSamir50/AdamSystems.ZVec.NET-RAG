using Microsoft.Extensions.VectorData;
using Xunit;

namespace ZVec.Extensions.VectorData.ConformanceTests;

/// <summary>
/// Sample POCO model for M.E.VectorData attribute reflection tests.
/// </summary>
public sealed class SampleVectorRecord
{
    /// <summary>Document Key.</summary>
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>Text Payload Field.</summary>
    [VectorStoreData(IsIndexed = true, IsFullTextIndexed = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Vector Embedding Field.</summary>
    [VectorStoreVector(768)]
    public ReadOnlyMemory<float> Vector { get; set; }
}

/// <summary>
/// Tests that the sample POCO is correctly annotated with [VectorStoreKey], [VectorStoreData],
/// [VectorStoreVector] attributes — used for attribute reflection validation, NOT for VectorData contract conformance.
/// </summary>
public sealed class VectorRecordAttributeReflectionTests
{
    /// <summary>
    /// Verifies that property metadata reader correctly extracts attributes for key, data, and vector properties.
    /// </summary>
    [Fact]
    public void VectorRecordClass_DeclaresAllRequiredVectorStoreAttributes()
    {
        var recordType = typeof(SampleVectorRecord);

        Assert.True(recordType.IsClass, "Vector record must be a class.");

        var properties = recordType.GetProperties();
        Assert.Equal(3, properties.Length);

        var keyProp = Array.Find(properties, p => Attribute.IsDefined(p, typeof(VectorStoreKeyAttribute)));
        Assert.NotNull(keyProp);
        Assert.Equal(nameof(SampleVectorRecord.Id), keyProp.Name);

        var dataProp = Array.Find(properties, p => Attribute.IsDefined(p, typeof(VectorStoreDataAttribute)));
        Assert.NotNull(dataProp);
        Assert.Equal(nameof(SampleVectorRecord.Content), dataProp.Name);

        var vectorProp = Array.Find(properties, p => Attribute.IsDefined(p, typeof(VectorStoreVectorAttribute)));
        Assert.NotNull(vectorProp);
        Assert.Equal(nameof(SampleVectorRecord.Vector), vectorProp.Name);
    }
}
