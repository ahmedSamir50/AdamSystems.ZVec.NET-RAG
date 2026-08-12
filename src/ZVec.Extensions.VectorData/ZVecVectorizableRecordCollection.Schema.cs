using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData.Attributes;
using ZVec.NET;
using ZVec.NET.Mapping;
using ZVec.NET.Query;

namespace ZVec.Extensions.VectorData;

/// <summary>
/// Schema building, full-text field resolution, and native collection open logic
/// for <see cref="ZVecVectorizableRecordCollection{TRecord, TKey}"/>.
/// </summary>
public sealed partial class ZVecVectorizableRecordCollection<TRecord, TKey>
    where TRecord : class
    where TKey : notnull
{
    private ZVecCollectionSchema BuildCollectionSchema()
    {
        var schemaBuilder = ZVecCollectionSchemaBuilder.From<TRecord>();
        var schema = schemaBuilder.Build();

        var ftsFieldNames = new HashSet<string>(StringComparer.Ordinal);
        var ftsVectors = new List<ZVecVectorSchema>(schema.Vectors);

        foreach (var field in schema.Fields)
        {
            if (field.DataType != ZVecDataType.String)
                continue;

            var prop = typeof(TRecord).GetProperty(field.Name);
            if (prop == null || !IsFullTextIndexedProperty(prop) || ftsVectors.Any(v => v.Name == field.Name))
                continue;

            ftsFieldNames.Add(field.Name);
            ftsVectors.Add(new ZVecVectorSchema
            {
                Name = field.Name,
                DataType = ZVecDataType.String,
                Dimension = 0,
                IndexParam = new ZVecFtsIndexParam()
            });
        }

        var updatedFields = schema.Fields.Where(f => !ftsFieldNames.Contains(f.Name)).ToArray();

        return new ZVecCollectionSchema
        {
            Name = schema.Name,
            MaxDocCountPerSegment = schema.MaxDocCountPerSegment,
            Fields = updatedFields,
            Vectors = ftsVectors.ToArray()
        };
    }

    /// <summary>
    /// Resolves whether a record property participates in full-text search indexing.
    /// </summary>
    /// <remarks>
    /// <c>[ZVecFullTextSearch]</c> takes precedence. <c>[VectorStoreData(IsFullTextIndexed = true)]</c>
    /// is recognized as a fallback when no ZVec FTS attribute is present.
    /// </remarks>
    private static bool IsFullTextIndexedProperty(PropertyInfo prop)
    {
        var zvecFtsAttr = (ZVecFullTextSearchAttribute?)Attribute.GetCustomAttribute(prop, typeof(ZVecFullTextSearchAttribute));
        if (zvecFtsAttr != null)
            return zvecFtsAttr.IsFullTextIndexed;

        var vectorDataAttr = (VectorStoreDataAttribute?)Attribute.GetCustomAttribute(prop, typeof(VectorStoreDataAttribute));
        return vectorDataAttr?.IsFullTextIndexed == true;
    }

    /// <summary>
    /// Resolves the native storage name of the first scalar field marked for full-text
    /// search indexing. Falls back to the first scalar field (or "Content") when no field
    /// is explicitly FTS-indexed, preserving prior behavior for collections without
    /// FTS attributes.
    /// </summary>
    private string ResolveFullTextField()
    {
        if (_typeModel != null)
        {
            foreach (var field in _typeModel.Fields)
            {
                if (field.Property != null && IsFullTextIndexedProperty(field.Property))
                    return field.StorageName;
            }

            if (_typeModel.Fields.FirstOrDefault() is { } firstField)
                return firstField.StorageName;
        }

        return "Content";
    }

    /// <summary>
    /// Extracts the CLR property name from a property-selector expression such as
    /// <c>x =&gt; x.Content</c>. Returns null when the expression is null or does not
    /// reference a single property. The returned CLR name is later matched against the
    /// type model's storage names by the caller as needed.
    /// </summary>
    private static string? TryGetPropertyName(LambdaExpression? selector)
    {
        if (selector?.Body is MemberExpression member && member.Member is PropertyInfo)
            return member.Member.Name;
        return null;
    }

    private IZvecCollection OpenNativeCollection()
    {
        if (!_factory.IsInitialized)
            _factory.Initialize();

        Directory.CreateDirectory(_options.EffectiveCollectionBasePath);
        return _factory.OpenOrCreate(CollectionPath, BuildCollectionSchema());
    }
}
