using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using ZVec.NET;
using Xunit;

namespace ZVec.Extensions.VectorData.Tests;

/// <summary>
/// Unit tests verifying Dependency Injection registration via ZVecVectorStoreServiceCollectionExtensions.
/// </summary>
public sealed class ZVecVectorStoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddZVecVectorStore_ThrowsArgumentNullException_WhenServicesIsNull()
    {
        IServiceCollection services = null!;
        Assert.Throws<ArgumentNullException>(() => services.AddZVecVectorStore());
    }

    [Fact]
    public void AddZVecVectorStore_DefaultsMaxConcurrentNativeCallsToProcessorCount()
    {
        var services = new ServiceCollection();
        services.AddZVecVectorStore();

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<ZVecVectorStoreOptions>();

        Assert.Equal(Environment.ProcessorCount, options.MaxConcurrentNativeCalls);
    }

    [Fact]
    public void AddZVecVectorStore_AppliesCustomMaxConcurrentNativeCalls_WhenConfigured()
    {
        var services = new ServiceCollection();
        services.AddZVecVectorStore(options => options.MaxConcurrentNativeCalls = 2);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<ZVecVectorStoreOptions>();

        Assert.Equal(2, options.MaxConcurrentNativeCalls);
    }

    [Fact]
    public void AddZVecVectorStore_RegistersVectorStoreServices_WhenCalledWithDefaults()
    {
        var services = new ServiceCollection();

        services.AddZVecVectorStore();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetService<IZvecFactory>();
        var zvecStore = provider.GetService<ZVecVectorStore>();
        var vectorStore = provider.GetService<VectorStore>();

        Assert.NotNull(factory);
        Assert.NotNull(zvecStore);
        Assert.NotNull(vectorStore);
        Assert.Same(zvecStore, vectorStore);
    }

    [Fact]
    public void AddZVecVectorStore_AppliesCustomOptions_WhenProvided()
    {
        var services = new ServiceCollection();
        IZvecFactory customFactory = new ZVecFactory();

        services.AddZVecVectorStore(options =>
        {
            options.Factory = customFactory;
            options.StoragePath = "D:/data/zvec_test";
        });

        var provider = services.BuildServiceProvider();
        var resolvedFactory = provider.GetRequiredService<IZvecFactory>();

        Assert.Same(customFactory, resolvedFactory);
    }
}
