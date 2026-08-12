using System.Diagnostics.CodeAnalysis;
using ZVec.NET;
using ZVec.NET.Mapping;
using ZVec.NET.Query;

namespace ZVec.Extensions.VectorData;

/// <summary>
/// Record mapping (POCO &lt;-&gt; <see cref="ZVecDoc"/>) and score normalization
/// for <see cref="ZVecVectorizableRecordCollection{TRecord, TKey}"/>.
/// </summary>
public sealed partial class ZVecVectorizableRecordCollection<TRecord, TKey>
    where TRecord : class
    where TKey : notnull
{
    /// <summary>
    /// Normalizes a native ZVec score into a similarity score where higher = better match.
    /// Switches on the configured <see cref="ZVecMetricType"/> for the collection.
    /// </summary>
    private float NormalizeScore(float nativeScore)
    {
        var indexParam = _typeModel?.Vectors.FirstOrDefault()?.IndexParam;
        ZVecMetricType metric = (indexParam as ZVecHnswIndexParam)?.MetricType ?? ZVecMetricType.Cosine;

        return metric switch
        {
            ZVecMetricType.Cosine => 1.0f - nativeScore,
            ZVecMetricType.L2 => 1.0f / (1.0f + nativeScore),
            ZVecMetricType.Ip => nativeScore,
            _ => 1.0f - nativeScore
        };
    }

    [RequiresUnreferencedCode("Source generated mappers should be used for Native AOT. Reflection fallback may be trimmed.")]
    [RequiresDynamicCode("Reflection fallback requires dynamic code generation.")]
    private ZVecDoc MapToDoc(TRecord record)
    {
        if (_mapper != null)
        {
            return _mapper.ToDoc(record, _typeModel!);
        }
        return ZVecMapper.ToDoc(record, _typeModel!);
    }

    [RequiresUnreferencedCode("Source generated mappers should be used for Native AOT. Reflection fallback may be trimmed.")]
    [RequiresDynamicCode("Reflection fallback requires dynamic code generation.")]
    private TRecord MapFromDoc(ZVecDoc doc)
    {
        if (_typeModel == null) throw new InvalidOperationException("Type model is uninitialized.");

        if (_mapper != null)
        {
            return _mapper.FromDoc(doc, _typeModel);
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
