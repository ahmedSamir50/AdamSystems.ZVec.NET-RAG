using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData.Attributes;
using ZVec.Extensions.VectorData.Constants;
using ZVec.Extensions.VectorData.Mapping;
using ZVec.Extensions.VectorData.Store;
using ZVec.NET;
using ZVec.NET.Mapping;
using ZVec.NET.Query;

namespace ZVec.Extensions.VectorData.Collection;

/// <summary>
/// Schema building, full-text field resolution, and native collection open logic
/// for <see cref="ZVecVectorizableRecordCollection{TRecord, TKey}"/>.
/// </summary>
public sealed partial class ZVecVectorizableRecordCollection<TRecord, TKey>
    where TRecord : class
    where TKey : notnull
{
    /// <summary>
    /// Builds the native collection schema using the following precedence:
    /// <list type="number">
    /// <item><description>Source-generated zero-reflection schema factory (preferred for AOT).</description></item>
    /// <item><description>Caller-supplied <see cref="Definition"/> mapped via <see cref="ZVecVectorDataSchemaBuilder"/>.</description></item>
    /// <item><description>Annotated reflection fallback via <see cref="ZVecCollectionSchemaBuilder.From{TRecord}"/>.</description></item>
    /// </list>
    /// </summary>
    private ZVecCollectionSchema BuildCollectionSchema()
    {
        var generatedFactory = ZVecCollectionSchemaRegistry.Get<TRecord>();
        if (generatedFactory != null)
        {
            return FinalizeCollectionSchema(generatedFactory(Name));
        }

        if (Definition != null)
        {
            return ZVecVectorIndexResolver.ApplyStoreVectorOptions(
                ZVecVectorDataSchemaBuilder.BuildFromDefinition(Name, Definition, _options),
                _options);
        }

        return ZVecVectorIndexResolver.ApplyStoreVectorOptions(BuildCollectionSchemaFromReflection(), _options);
    }

    private ZVecCollectionSchema FinalizeCollectionSchema(ZVecCollectionSchema schema) =>
        ZVecVectorIndexResolver.ApplyStoreVectorOptions(schema, _options);

    [RequiresUnreferencedCode("Reflection-based schema building may be trimmed under Native AOT. Use the source generator or supply a VectorStoreCollectionDefinition.")]
    [RequiresDynamicCode("Reflection-based schema building requires dynamic code generation.")]
    private ZVecCollectionSchema BuildCollectionSchemaFromReflection()
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

        if (Definition != null)
        {
            foreach (var property in Definition.Properties.OfType<VectorStoreDataProperty>())
            {
                if (property.IsFullTextIndexed)
                {
                    return ZVecVectorDataSchemaBuilder.ResolveStorageName(property, property.Name);
                }
            }

            if (Definition.Properties.OfType<VectorStoreDataProperty>().FirstOrDefault() is { } firstData)
            {
                return ZVecVectorDataSchemaBuilder.ResolveStorageName(firstData, firstData.Name);
            }
        }

        return ZVecConstants.DefaultFullTextFieldName;
    }

    /// <summary>
    /// Resolves the native dense vector field storage name from the type model, generated
    /// definition, or connector defaults.
    /// </summary>
    private string ResolveVectorFieldName(string? optionsVectorProperty = null)
    {
        if (!string.IsNullOrEmpty(optionsVectorProperty))
            return optionsVectorProperty;

        if (_typeModel?.Vectors.FirstOrDefault() is { } typeModelVector)
            return typeModelVector.StorageName;

        if (Definition?.Properties.OfType<VectorStoreVectorProperty>().FirstOrDefault() is { } definitionVector)
            return ZVecVectorDataSchemaBuilder.ResolveStorageName(definitionVector, definitionVector.Name);

        return ZVecConstants.DefaultVectorFieldName;
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
        {
            _factory.Initialize(_options.CreateZVecOptions());
        }

        Directory.CreateDirectory(_options.EffectiveCollectionBasePath);
        return _factory.OpenOrCreate(
            CollectionPath,
            BuildCollectionSchema(),
            _options.CreateZVecCollectionOptions());
    }
}
