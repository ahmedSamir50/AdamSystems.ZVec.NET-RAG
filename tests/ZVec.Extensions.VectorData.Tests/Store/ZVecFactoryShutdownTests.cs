using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.VectorData;
using ZVec.NET;
using ZVec.NET.Mapping;
using Xunit;

namespace ZVec.Extensions.VectorData.Tests;

/// <summary>
/// Tests for <see cref="ZVecFactoryShutdownRegistration"/> and GC finalizer stress.
/// </summary>
public sealed class ZVecFactoryShutdownTests
{
    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "ZVecTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ApplicationStopping_InvokesFactoryShutdown_WhenHostedServiceStarts()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationLifetime>(lifetime);
        services.AddZVecVectorStore();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IZvecFactory>();
        Assert.True(factory.IsInitialized);

        foreach (var hosted in provider.GetServices<IHostedService>())
        {
            await hosted.StartAsync(TestContext.Current.CancellationToken);
        }

        lifetime.TriggerApplicationStopping();
        Assert.False(factory.IsInitialized);
    }

    [Fact]
    public async Task FinalizerStress_1000CollectionHandles_SurvivesGcWithoutDeadlock()
    {
        string storagePath = CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);
        var options = new ZVecVectorStoreOptions { StoragePath = storagePath };
        var factory = new ZVecFactory();
        factory.Initialize();

        try
        {
            var collections = new List<ZVecVectorizableRecordCollection<ShutdownStressRecord, string>>(1000);
            for (int i = 0; i < 1000; i++)
            {
                var collection = new ZVecVectorizableRecordCollection<ShutdownStressRecord, string>(
                    factory, options, "stress_" + i.ToString("D4"));
                await collection.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);
                collections.Add(collection);
            }

            collections.Clear();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var recovery = new ZVecVectorizableRecordCollection<ShutdownStressRecord, string>(
                factory, options, "recovery_col");
            await recovery.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);
            await recovery.EnsureCollectionDeletedAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            factory.Shutdown();
            if (Directory.Exists(storagePath))
            {
                try { Directory.Delete(storagePath, recursive: true); } catch { }
            }
        }
    }

    private sealed class ShutdownStressRecord
    {
        [ZVecId]
        [VectorStoreKey]
        public string Id { get; set; } = string.Empty;

        [ZVecVector(4)]
        [VectorStoreVector(4)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }

    private sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public CancellationToken ApplicationStarted => _started.Token;
        public CancellationToken ApplicationStopping => _stopping.Token;
        public CancellationToken ApplicationStopped => _stopped.Token;

        public void TriggerApplicationStopping()
        {
            _stopping.Cancel();
        }

        public void StopApplication() => TriggerApplicationStopping();
    }
}
