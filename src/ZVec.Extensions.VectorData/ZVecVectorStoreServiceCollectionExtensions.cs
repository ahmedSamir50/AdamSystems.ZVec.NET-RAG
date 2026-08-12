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

        services.TryAddSingleton<IZvecFactory>(sp => options.Factory ?? new ZVecFactory());

        var descriptor = ServiceDescriptor.Describe(
            typeof(ZVecVectorStore),
            sp => new ZVecVectorStore(sp.GetRequiredService<IZvecFactory>()),
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
