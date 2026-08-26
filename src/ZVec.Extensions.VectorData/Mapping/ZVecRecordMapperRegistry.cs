using System.Collections.Concurrent;

namespace ZVec.Extensions.VectorData.Mapping;

/// <summary>
/// Process-wide registry of AOT-clean record mappers emitted by the source generator.
/// Mappers self-register via <c>[ModuleInitializer]</c> at assembly load time —
/// no runtime reflection, no <c>Activator.CreateInstance</c>.
/// </summary>
public static class ZVecRecordMapperRegistry
{
    private static readonly ConcurrentDictionary<Type, object> _mappers = new();

    /// <summary>
    /// Registers a mapper for type <typeparamref name="TRecord"/>. Called from
    /// <c>[ModuleInitializer]</c> methods emitted by the source generator.
    /// </summary>
    public static void Register<TRecord>(IZVecRecordMapper<TRecord> mapper) where TRecord : class
    {
        if (mapper == null) throw new ArgumentNullException(nameof(mapper));
        _mappers[typeof(TRecord)] = mapper;
    }

    /// <summary>
    /// Returns the registered mapper for <typeparamref name="TRecord"/>, or <c>null</c>
    /// if no mapper has been registered (e.g. for <c>Dictionary&lt;string, object?&gt;</c> dynamic collections).
    /// </summary>
    public static IZVecRecordMapper<TRecord>? Get<TRecord>() where TRecord : class
    {
        return _mappers.TryGetValue(typeof(TRecord), out var mapper)
            ? (IZVecRecordMapper<TRecord>)mapper
            : null;
    }
}
