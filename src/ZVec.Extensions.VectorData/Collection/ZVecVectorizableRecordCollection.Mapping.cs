using System.Diagnostics.CodeAnalysis;
using ZVec.Extensions.VectorData.Constants;
using ZVec.NET;
using ZVec.NET.Mapping;

namespace ZVec.Extensions.VectorData.Collection;

/// <summary>
/// Record mapping (POCO &lt;-&gt; <see cref="ZVecDoc"/>) and score normalization
/// for <see cref="ZVecVectorizableRecordCollection{TRecord, TKey}"/>.
/// </summary>
public sealed partial class ZVecVectorizableRecordCollection<TRecord, TKey>
    where TRecord : class
    where TKey : notnull
{
    /// <summary>
    /// Normalizes a native dense-query distance into a VectorData similarity score.
    /// </summary>
    private float NormalizeDenseScore(float nativeDistance)
    {
        ZVecMetricType metric = ResolveDenseMetricType();
        return ZVecScoreNormalizer.ToSimilarity(nativeDistance, metric);
    }

    /// <summary>
    /// Resolves the metric type for the primary dense vector index from the reflection type model
    /// or the source-generated / definition schema (cached after first resolution).
    /// </summary>
    private ZVecMetricType ResolveDenseMetricType()
    {
        if (_typeModel?.Vectors.FirstOrDefault()?.IndexParam is ZVecHnswIndexParam hnswFromModel)
        {
            return hnswFromModel.MetricType;
        }

        if (TryGetCachedDenseIndexParam() is ZVecHnswIndexParam hnswFromSchema)
        {
            return hnswFromSchema.MetricType;
        }

        return ZVecMetricType.Cosine;
    }

    private ZVecIndexParam? TryGetCachedDenseIndexParam()
    {
        if (_cachedDenseIndexParam != null)
        {
            return _cachedDenseIndexParam;
        }

        var schema = BuildCollectionSchema();
        _cachedDenseIndexParam = schema.Vectors.FirstOrDefault(v => v.Dimension > 0)?.IndexParam;
        return _cachedDenseIndexParam;
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Reflection mapping fallback is only used for dynamic Dictionary collections; AOT apps use source-generated mappers.")]
    private ZVecDoc MapToDoc(TRecord record)
    {
        if (_mapper != null)
        {
            return _mapper.ToDoc(record, _typeModel!);
        }

        return MapToDocReflection(record);
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Reflection mapping fallback is only used for dynamic Dictionary collections; AOT apps use source-generated mappers.")]
    private TRecord MapFromDoc(ZVecDoc doc)
    {
        if (_mapper != null)
        {
            return _mapper.FromDoc(doc, _typeModel!);
        }

        return MapFromDocReflection(doc);
    }

    [RequiresUnreferencedCode("Source generated mappers should be used for Native AOT. Reflection fallback may be trimmed.")]
    [RequiresDynamicCode("Reflection fallback requires dynamic code generation.")]
    private ZVecDoc MapToDocReflection(TRecord record)
    {
        if (_typeModel == null)
        {
            throw new InvalidOperationException(ZVecErrorMessages.TypeModelUninitialized);
        }

        return ZVecMapper.ToDoc(record, _typeModel);
    }

    [RequiresUnreferencedCode("Source generated mappers should be used for Native AOT. Reflection fallback may be trimmed.")]
    [RequiresDynamicCode("Reflection fallback requires dynamic code generation.")]
    private TRecord MapFromDocReflection(ZVecDoc doc)
    {
        if (_typeModel == null)
        {
            throw new InvalidOperationException(ZVecErrorMessages.TypeModelUninitialized);
        }

        // Reflection fallback — only used for Dictionary<string, object?> dynamic collections
        // or when SG mapper is not generated (e.g. during early development).
        var record = (TRecord)Activator.CreateInstance(typeof(TRecord))!;
        _typeModel.Id.Property.SetValue(record, doc.Id);
        foreach (var field in _typeModel.Fields)
        {
            if (doc.Fields.TryGetValue(field.StorageName, out var val) && val != null)
            {
                field.Property.SetValue(record, val);
            }
        }

        foreach (var vec in _typeModel.Vectors)
        {
            if (doc.DenseVectors.TryGetValue(vec.StorageName, out var dense))
            {
                vec.Property.SetValue(record, dense);
            }
        }

        return record;
    }
}
