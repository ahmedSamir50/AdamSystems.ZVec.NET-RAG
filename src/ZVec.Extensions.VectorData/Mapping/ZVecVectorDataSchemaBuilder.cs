using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData.Store;
using ZVec.NET;

namespace ZVec.Extensions.VectorData.Mapping;

/// <summary>
/// Builds native <see cref="ZVecCollectionSchema"/> instances from Microsoft.Extensions.VectorData
/// collection definitions without relying on CLR reflection over record POCOs.
/// </summary>
public static class ZVecVectorDataSchemaBuilder
{
    /// <summary>
    /// Builds a native collection schema from a <see cref="VectorStoreCollectionDefinition"/>.
    /// </summary>
    /// <param name="collectionName">Native collection name.</param>
    /// <param name="definition">VectorData collection definition describing key, data, and vector properties.</param>
    /// <param name="options">Optional vector store options controlling default quantization.</param>
    /// <returns>A fully constructed <see cref="ZVecCollectionSchema"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is null.</exception>
    public static ZVecCollectionSchema BuildFromDefinition(
        string collectionName,
        VectorStoreCollectionDefinition definition,
        ZVecVectorStoreOptions? options = null)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));

        options ??= new ZVecVectorStoreOptions();
        var builder = new ZVecCollectionSchemaBuilder(collectionName);

        foreach (var property in definition.Properties)
        {
            switch (property)
            {
                case VectorStoreVectorProperty vectorProperty:
                    var quantizeType = ZVecVectorIndexResolver.ResolveQuantizeType(vectorProperty, options);
                    builder.AddVector(
                        ResolveStorageName(vectorProperty, vectorProperty.Name),
                        ZVecVectorIndexResolver.ResolveVectorDataType(vectorProperty.EmbeddingType),
                        vectorProperty.Dimensions,
                        ZVecVectorIndexResolver.CreateHnswIndexParam(quantizeType));
                    break;

                case VectorStoreDataProperty dataProperty:
                    string dataStorageName = ResolveStorageName(dataProperty, dataProperty.Name);
                    if (dataProperty.IsFullTextIndexed && dataProperty.Type == typeof(string))
                    {
                        builder.AddVector(new ZVecVectorSchema
                        {
                            Name = dataStorageName,
                            DataType = ZVecDataType.String,
                            Dimension = 0,
                            IndexParam = new ZVecFtsIndexParam()
                        });
                    }
                    else
                    {
                        var propertyType = dataProperty.Type ?? typeof(string);
                        builder.AddField(
                            dataStorageName,
                            MapClrTypeToZVecDataType(propertyType),
                            nullable: true,
                            dataProperty.IsIndexed ? new ZVecInvertIndexParam() : null);
                    }

                    break;

                case VectorStoreKeyProperty:
                    // Primary keys are stored on ZVecDoc.Id — not modeled as scalar fields.
                    break;
            }
        }

        return builder.Build();
    }

    /// <summary>
    /// Resolves the storage name for a VectorData property, preferring an explicit
    /// <see cref="VectorStoreProperty.StorageName"/> override when supplied.
    /// </summary>
    internal static string ResolveStorageName(VectorStoreProperty property, string propertyName)
        => string.IsNullOrWhiteSpace(property.StorageName) ? propertyName : property.StorageName;

    /// <summary>
    /// Maps a CLR property type to the closest native <see cref="ZVecDataType"/> scalar.
    /// </summary>
    internal static ZVecDataType MapClrTypeToZVecDataType(Type propertyType)
    {
        if (propertyType == typeof(string)) return ZVecDataType.String;
        if (propertyType == typeof(bool)) return ZVecDataType.Bool;
        if (propertyType == typeof(int)) return ZVecDataType.Int32;
        if (propertyType == typeof(long)) return ZVecDataType.Int64;
        if (propertyType == typeof(uint)) return ZVecDataType.UInt32;
        if (propertyType == typeof(ulong)) return ZVecDataType.UInt64;
        if (propertyType == typeof(float)) return ZVecDataType.Float;
        if (propertyType == typeof(double)) return ZVecDataType.Double;
        if (propertyType == typeof(Guid)) return ZVecDataType.String;
        return ZVecDataType.String;
    }
}
