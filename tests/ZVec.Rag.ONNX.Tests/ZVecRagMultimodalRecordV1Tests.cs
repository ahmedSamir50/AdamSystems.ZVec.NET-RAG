using System.Reflection;
using Microsoft.Extensions.VectorData;
using ZVec.NET.Mapping;
using ZVec.Rag.ONNX;
using ZVec.Rag.ONNX.Schema;

namespace ZVec.Rag.ONNX.Tests;

public sealed class ZVecRagMultimodalRecordV1Tests
{
    [Fact]
    public void SourceKind_DefaultsToText()
    {
        var record = new ZVecRagMultimodalRecordV1();
        Assert.Equal(OnnxConstants.SourceKindText, record.SourceKind);
    }

    [Fact]
    public void SourceKind_HasDualVectorStoreAndZVecAttributes()
    {
        PropertyInfo property = typeof(ZVecRagMultimodalRecordV1).GetProperty(nameof(ZVecRagMultimodalRecordV1.SourceKind))!;
        Assert.NotNull(property.GetCustomAttribute<VectorStoreDataAttribute>());
        Assert.NotNull(property.GetCustomAttribute<ZVecFieldAttribute>());
    }
}
