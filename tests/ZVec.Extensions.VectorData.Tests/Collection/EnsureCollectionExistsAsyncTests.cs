using Xunit;
using ZVec.NET;

namespace ZVec.Extensions.VectorData.Tests;

/// <summary>
/// ForceYielding and native-open thread occupancy for <see cref="ZVecVectorizableRecordCollection{TRecord, TKey}.EnsureCollectionExistsAsync"/>.
/// </summary>
public sealed class EnsureCollectionExistsAsyncTests
{
    private static ZVecVectorStoreOptions CreateOptions()
        => new() { StoragePath = Path.Combine(Path.GetTempPath(), "ZVecTests", Guid.NewGuid().ToString("N")) };

    [Fact]
    public async Task EnsureCollectionExistsAsync_OpensOffSynchronizationContextThread_AfterForceYielding()
    {
        var options = CreateOptions();
        var factory = new OpenThreadRecordingFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(
            factory, options, "force_yield_open");

        int contextThreadId = Environment.CurrentManagedThreadId;
        var syncContext = new InlineSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(syncContext);

        try
        {
            await collection.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);
            Assert.NotEqual(contextThreadId, factory.OpenThreadId);
            Assert.True(factory.OpenThreadId > 0);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(null);
            factory.Shutdown();
            if (Directory.Exists(options.StoragePath))
            {
                try { Directory.Delete(options.StoragePath, recursive: true); } catch { }
            }
        }
    }

    /// <summary>Runs posted work inline on the installing thread (simulates UI/main thread).</summary>
    private sealed class InlineSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => d(state);

        public override void Send(SendOrPostCallback d, object? state) => d(state);
    }

    private sealed class OpenThreadRecordingFactory : IZvecFactory
    {
        private readonly ZVecFactory _inner = new();

        public int OpenThreadId { get; private set; }

        public bool IsInitialized => _inner.IsInitialized;

        public void Initialize(ZVecOptions? options = null) => _inner.Initialize(options);

        public ValueTask InitializeAsync(ZVecOptions? options = null, CancellationToken cancellationToken = default) =>
            _inner.InitializeAsync(options, cancellationToken);

        public void Shutdown() => _inner.Shutdown();

        public ValueTask ShutdownAsync(CancellationToken cancellationToken = default) =>
            _inner.ShutdownAsync(cancellationToken);

        public IZvecCollection CreateAndOpen(string path, ZVecCollectionSchema schema, ZVecCollectionOptions? options = null) =>
            _inner.CreateAndOpen(path, schema, options);

        public ValueTask<IZvecCollection> CreateAndOpenAsync(
            string path,
            ZVecCollectionSchema schema,
            ZVecCollectionOptions? options = null,
            CancellationToken cancellationToken = default) =>
            _inner.CreateAndOpenAsync(path, schema, options, cancellationToken);

        public IZvecCollection Open(string path, ZVecCollectionOptions? options = null) =>
            _inner.Open(path, options);

        public ValueTask<IZvecCollection> OpenAsync(
            string path,
            ZVecCollectionOptions? options = null,
            CancellationToken cancellationToken = default) =>
            _inner.OpenAsync(path, options, cancellationToken);

        public IZvecCollection OpenOrCreate(string path, ZVecCollectionSchema schema, ZVecCollectionOptions? options = null)
        {
            OpenThreadId = Environment.CurrentManagedThreadId;
            return _inner.OpenOrCreate(path, schema, options);
        }

        public ValueTask<IZvecCollection> OpenOrCreateAsync(
            string path,
            ZVecCollectionSchema schema,
            ZVecCollectionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            OpenThreadId = Environment.CurrentManagedThreadId;
            return _inner.OpenOrCreateAsync(path, schema, options, cancellationToken);
        }

        public string GetNativeVersion() => _inner.GetNativeVersion();

        public ZVecNativeAbiInfo GetAbiInfo() => _inner.GetAbiInfo();

        public void Dispose() => _inner.Dispose();

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}
