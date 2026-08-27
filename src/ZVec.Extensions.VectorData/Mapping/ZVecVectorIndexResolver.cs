using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData.Store;
using ZVec.NET;

namespace ZVec.Extensions.VectorData.Mapping;

/// <summary>
/// Resolves dense vector storage types and HNSW index parameters from
/// <see cref="VectorStoreVectorProperty"/> metadata and <see cref="ZVecVectorStoreOptions"/>.
/// </summary>
public static class ZVecVectorIndexResolver
{
    /// <summary>
    /// Maps a VectorData <see cref="VectorStoreVectorProperty.EmbeddingType"/> to the native
    /// <see cref="ZVecDataType"/> storage format. Defaults to FP32 when unset.
    /// </summary>
    /// <param name="embeddingType">Optional embedding CLR type from the vector property definition.</param>
    /// <returns>Native vector storage data type.</returns>
    public static ZVecDataType ResolveVectorDataType(Type? embeddingType)
    {
        if (embeddingType == typeof(Half))
        {
            return ZVecDataType.VectorFp16;
        }

        return ZVecDataType.VectorFp32;
    }

    /// <summary>
    /// Creates a default HNSW index parameter with the supplied quantization mode and metric.
    /// </summary>
    /// <param name="quantizeType">Quantization applied at index build/query time.</param>
    /// <param name="metricType">Dense vector distance metric.</param>
    /// <returns>A configured <see cref="ZVecHnswIndexParam"/> instance.</returns>
    public static ZVecHnswIndexParam CreateHnswIndexParam(
        ZVecQuantizeType quantizeType = ZVecQuantizeType.Undefined,
        ZVecMetricType metricType = ZVecMetricType.Cosine) =>
        new() { QuantizeType = quantizeType, MetricType = metricType };

    /// <summary>
    /// Resolves the dense vector metric from a VectorData vector property's <c>DistanceFunction</c>
    /// or legacy <c>IndexKind</c> metric name when supplied.
    /// </summary>
    public static ZVecMetricType ResolveMetricType(VectorStoreVectorProperty vectorProperty) =>
        ZVecVectorDistanceFunctionMapper.ToMetricType(vectorProperty);

    /// <summary>
    /// Builds HNSW index parameters from VectorData vector metadata and store options.
    /// </summary>
    public static ZVecHnswIndexParam CreateHnswIndexParam(
        VectorStoreVectorProperty vectorProperty,
        ZVecVectorStoreOptions options) =>
        CreateHnswIndexParam(
            ResolveQuantizeType(vectorProperty, options),
            ResolveMetricType(vectorProperty));

    /// <summary>
    /// Applies store-level vector index defaults (e.g. <see cref="ZVecVectorStoreOptions.DefaultQuantizeType"/>)
    /// to every HNSW index parameter on the supplied schema.
    /// </summary>
    /// <param name="schema">Collection schema returned from generated or definition builders.</param>
    /// <param name="options">Active vector store options.</param>
    /// <returns>The same <paramref name="schema"/> instance for chaining.</returns>
    public static ZVecCollectionSchema ApplyStoreVectorOptions(ZVecCollectionSchema schema, ZVecVectorStoreOptions options)
    {
        if (options.DefaultQuantizeType == ZVecQuantizeType.Undefined)
        {
            return schema;
        }

        var updatedVectors = new ZVecVectorSchema[schema.Vectors.Count];
        for (int i = 0; i < schema.Vectors.Count; i++)
        {
            var vector = schema.Vectors[i];
            if (vector.IndexParam is ZVecHnswIndexParam hnsw)
            {
                updatedVectors[i] = new ZVecVectorSchema
                {
                    Name = vector.Name,
                    DataType = vector.DataType,
                    Dimension = vector.Dimension,
                    IndexParam = new ZVecHnswIndexParam
                    {
                        MetricType = hnsw.MetricType,
                        M = hnsw.M,
                        EfConstruction = hnsw.EfConstruction,
                        QuantizeType = options.DefaultQuantizeType
                    }
                };
            }
            else
            {
                updatedVectors[i] = vector;
            }
        }

        return new ZVecCollectionSchema
        {
            Name = schema.Name,
            MaxDocCountPerSegment = schema.MaxDocCountPerSegment,
            Fields = schema.Fields,
            Vectors = updatedVectors
        };
    }

    /// <summary>
    /// Resolves the effective quantization type for a vector property, preferring an explicit
    /// per-definition override encoded in <see cref="VectorStoreVectorProperty.IndexKind"/> when it
    /// matches a <see cref="ZVecQuantizeType"/> name (case-insensitive); otherwise falls back to
    /// <see cref="ZVecVectorStoreOptions.DefaultQuantizeType"/>.
    /// </summary>
    internal static ZVecQuantizeType ResolveQuantizeType(
        VectorStoreVectorProperty vectorProperty,
        ZVecVectorStoreOptions options)
    {
        if (!string.IsNullOrWhiteSpace(vectorProperty.IndexKind) &&
            Enum.TryParse(vectorProperty.IndexKind, ignoreCase: true, out ZVecQuantizeType parsed))
        {
            return parsed;
        }

        return options.DefaultQuantizeType;
    }
}
