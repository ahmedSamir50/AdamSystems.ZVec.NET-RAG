using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData.Constants;
using ZVec.Extensions.VectorData.Exceptions;
using ZVec.Extensions.VectorData.Mapping;
using ZVec.NET.Exceptions;
using ZVec.NET.Mapping;

namespace ZVec.Extensions.VectorData.Filter;

/// <summary>
/// Resolves CLR property names to native storage names for filter expression translation.
/// Prefers <see cref="ZVecTypeModel"/> when ZVec attributes are present; otherwise uses
/// source-generated or caller-supplied <see cref="VectorStoreCollectionDefinition"/> metadata.
/// </summary>
internal sealed class ZVecFilterRecordModel
{
    private readonly ZVecTypeModel? _typeModel;
    private readonly Dictionary<string, string>? _storageByProperty;

    private ZVecFilterRecordModel(ZVecTypeModel typeModel)
    {
        _typeModel = typeModel;
    }

    private ZVecFilterRecordModel(Dictionary<string, string> storageByProperty)
    {
        _storageByProperty = storageByProperty;
    }

    /// <summary>
    /// Resolves filter metadata for <typeparamref name="TRecord"/>.
    /// </summary>
    public static ZVecFilterRecordModel Resolve<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TRecord>()
        where TRecord : class
    {
        try
        {
            return FromTypeModel<TRecord>();
        }
        catch (ZVecException)
        {
            // Fall through to VectorData definition metadata.
        }

        var definition = ZVecCollectionSchemaRegistry.GetDefinition<TRecord>();
        if (definition != null)
        {
            return FromDefinition(definition);
        }

        throw new ZVecFilterTranslationException(
            ZVecErrorMessages.UnsupportedFilterExpression(
                $"Record type '{typeof(TRecord).Name}' has no ZVec attributes or registered VectorData definition for filter translation."));
    }

    /// <summary>
    /// Returns the native storage column name for the given CLR property.
    /// </summary>
    public string GetStorageName(string propertyName)
    {
        if (_typeModel != null)
        {
            return _typeModel.GetRequiredByPropertyName(propertyName).StorageName;
        }

        if (_storageByProperty != null &&
            _storageByProperty.TryGetValue(propertyName, out var storageName))
        {
            return storageName;
        }

        throw new ZVecFilterTranslationException(
            ZVecErrorMessages.UnsupportedFilterExpression(
                $"Property '{propertyName}' is not filterable (vector, full-text, or unknown field)."));
    }

    private static ZVecFilterRecordModel FromDefinition(VectorStoreCollectionDefinition definition)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in definition.Properties)
        {
            switch (property)
            {
                case VectorStoreKeyProperty keyProperty:
                    map[keyProperty.Name] = ZVecVectorDataSchemaBuilder.ResolveStorageName(keyProperty, keyProperty.Name);
                    break;

                case VectorStoreDataProperty dataProperty when !dataProperty.IsFullTextIndexed:
                    map[dataProperty.Name] = ZVecVectorDataSchemaBuilder.ResolveStorageName(dataProperty, dataProperty.Name);
                    break;

                case VectorStoreVectorProperty:
                    // Dense vectors are not scalar filter columns.
                    break;
            }
        }

        return new ZVecFilterRecordModel(map);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2091", Justification = "ZVecTypeModel path is only used for [ZVec*] annotated records; VectorData-only records use definition metadata.")]
    private static ZVecFilterRecordModel FromTypeModel<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TRecord>()
        where TRecord : class
    {
        return new ZVecFilterRecordModel(ZVecTypeModel.Get<TRecord>());
    }
}
