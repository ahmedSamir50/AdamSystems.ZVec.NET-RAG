using Microsoft.Extensions.VectorData;
using ZVec.NET;
using Xunit;

namespace ZVec.Extensions.VectorData.Tests;

/// <summary>
/// Unit tests for <see cref="ZVecVectorIndexResolver"/> schema resolution.
/// </summary>
public sealed class ZVecVectorIndexResolverTests
{
    [Fact]
    public void ResolveVectorDataType_ReturnsFp16_WhenEmbeddingTypeIsHalf()
    {
        Assert.Equal(ZVecDataType.VectorFp16, ZVecVectorIndexResolver.ResolveVectorDataType(typeof(Half)));
    }

    [Fact]
    public void ResolveVectorDataType_ReturnsFp32_WhenEmbeddingTypeIsNullOrFloat()
    {
        Assert.Equal(ZVecDataType.VectorFp32, ZVecVectorIndexResolver.ResolveVectorDataType(null));
        Assert.Equal(ZVecDataType.VectorFp32, ZVecVectorIndexResolver.ResolveVectorDataType(typeof(float)));
        Assert.Equal(ZVecDataType.VectorFp32, ZVecVectorIndexResolver.ResolveVectorDataType(typeof(ReadOnlyMemory<float>)));
    }

    [Fact]
    public void BuildFromDefinition_UsesFp16Storage_WhenEmbeddingTypeIsHalf()
    {
        var definition = new VectorStoreCollectionDefinition
        {
            Properties =
            [
                new VectorStoreKeyProperty("Id", typeof(string)),
                new VectorStoreVectorProperty("Embedding", typeof(ReadOnlyMemory<float>), 4)
                {
                    EmbeddingType = typeof(Half)
                }
            ]
        };

        var schema = ZVecVectorDataSchemaBuilder.BuildFromDefinition("quant_test", definition);

        Assert.Single(schema.Vectors);
        Assert.Equal(ZVecDataType.VectorFp16, schema.Vectors[0].DataType);
    }

    [Fact]
    public void ApplyStoreVectorOptions_SetsHnswQuantizeType_WhenDefaultConfigured()
    {
        var schema = new ZVecCollectionSchema
        {
            Name = "q",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "v",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecHnswIndexParam()
                }
            ]
        };

        var options = new ZVecVectorStoreOptions { DefaultQuantizeType = ZVecQuantizeType.Int8 };
        var result = ZVecVectorIndexResolver.ApplyStoreVectorOptions(schema, options);

        var hnsw = Assert.IsType<ZVecHnswIndexParam>(result.Vectors[0].IndexParam);
        Assert.Equal(ZVecQuantizeType.Int8, hnsw.QuantizeType);
    }
}
