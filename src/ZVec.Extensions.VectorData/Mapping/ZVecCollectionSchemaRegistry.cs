using System.Collections.Concurrent;
using Microsoft.Extensions.VectorData;
using ZVec.NET;

namespace ZVec.Extensions.VectorData.Mapping;

/// <summary>
/// Process-wide registry of AOT-clean collection schema factories emitted by the source generator.
/// Factories self-register via <c>[ModuleInitializer]</c> at assembly load time.
/// </summary>
public static class ZVecCollectionSchemaRegistry
{
    private static readonly ConcurrentDictionary<Type, Func<string, ZVecCollectionSchema>> _factories = new();
    private static readonly ConcurrentDictionary<Type, VectorStoreCollectionDefinition> _definitions = new();

    /// <summary>
    /// Registers a schema factory and optional VectorData definition for type <typeparamref name="TRecord"/>.
    /// </summary>
    public static void Register<TRecord>(
        Func<string, ZVecCollectionSchema> factory,
        VectorStoreCollectionDefinition? definition = null) where TRecord : class
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        _factories[typeof(TRecord)] = factory;
        if (definition != null)
        {
            _definitions[typeof(TRecord)] = definition;
        }
    }

    /// <summary>
    /// Returns the registered schema factory for <typeparamref name="TRecord"/>, or <c>null</c>
    /// when no factory has been registered.
    /// </summary>
    public static Func<string, ZVecCollectionSchema>? Get<TRecord>() where TRecord : class
    {
        return _factories.TryGetValue(typeof(TRecord), out var factory) ? factory : null;
    }

    /// <summary>
    /// Returns the source-generated <see cref="VectorStoreCollectionDefinition"/> for
    /// <typeparamref name="TRecord"/> when available.
    /// </summary>
    public static VectorStoreCollectionDefinition? GetDefinition<TRecord>() where TRecord : class
    {
        return _definitions.TryGetValue(typeof(TRecord), out var definition) ? definition : null;
    }
}
