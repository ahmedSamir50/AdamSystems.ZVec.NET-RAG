using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.VectorData;
using ZVec.NET;

namespace ZVec.Extensions.VectorData;

/// <summary>
/// Service collection extension methods for registering ZVec vector store services into DI containers.
/// </summary>
public static class ZVecVectorStoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="ZVecVectorStore"/> and <see cref="VectorStore"/> to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The target <see cref="IServiceCollection"/>.</param>
    /// <param name="configure">Optional configuration callback for <see cref="ZVecVectorStoreOptions"/>.</param>
    /// <param name="lifetime">The service lifetime for vector store registrations (defaults to <see cref="ServiceLifetime.Singleton"/>).</param>
    /// <returns>The modified <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <remarks>
    /// <b>Singleton lifetime is mandatory for <see cref="IZvecFactory"/>.</b> The native factory owns
    /// process-wide resources (file handles, mmap regions, P/Invoke SafeHandles) that must NOT be
    /// shared across multiple factory instances pointing at the same storage path.
    /// <see cref="ZVecVectorStore"/> is registered with the same lifetime as <paramref name="lifetime"/>.
    /// </remarks>
    public static IServiceCollection AddZVecVectorStore(
        this IServiceCollection services,
        Action<ZVecVectorStoreOptions>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        var options = new ZVecVectorStoreOptions();
        configure?.Invoke(options);

        services.TryAddSingleton<IZvecFactory>(sp =>
        {
            if (options.Factory != null)
            {
                if (!options.Factory.IsInitialized)
                {
                    options.Factory.Initialize();
                }
                return options.Factory;
            }

            var factory = new ZVecFactory();
            factory.Initialize();
            return factory;
        });

        // ZVecVectorStoreOptions registered as singleton so EffectiveCollectionBasePath
        // is computed once and shared by all ZVecVectorStore instances and collections.
        services.TryAddSingleton(options);

        var descriptor = ServiceDescriptor.Describe(
            typeof(ZVecVectorStore),
            sp => new ZVecVectorStore(
                sp.GetRequiredService<IZvecFactory>(),
                sp.GetRequiredService<ZVecVectorStoreOptions>()),
            lifetime);

        var abstractDescriptor = ServiceDescriptor.Describe(
            typeof(VectorStore),
            sp => sp.GetRequiredService<ZVecVectorStore>(),
            lifetime);

        services.TryAdd(descriptor);
        services.TryAdd(abstractDescriptor);

        return services;
    }
}
