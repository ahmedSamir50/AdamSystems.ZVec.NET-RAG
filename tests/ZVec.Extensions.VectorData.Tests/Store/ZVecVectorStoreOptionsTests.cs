using Microsoft.Extensions.VectorData;
using ZVec.NET;
using ZVec.NET.Mapping;
using Xunit;

namespace ZVec.Extensions.VectorData.Tests;

/// <summary>
/// Verifies <see cref="ZVecVectorStoreOptions"/> collection/engine option plumbing.
/// </summary>
public sealed class ZVecVectorStoreOptionsTests
{
    [Fact]
    public void Options_ExposeMmapReadOnlyMemoryLimitAndQuantizeDefaults()
    {
        var options = new ZVecVectorStoreOptions
        {
            MemoryLimitMb = 512,
            MaxConcurrentNativeCalls = 4,
            EnableMmap = false,
            ReadOnly = true,
            DefaultQuantizeType = ZVecQuantizeType.Int8
        };

        Assert.Equal(512, options.MemoryLimitMb);
        Assert.Equal(4, options.MaxConcurrentNativeCalls);
        Assert.False(options.EnableMmap);
        Assert.True(options.ReadOnly);
        Assert.Equal(ZVecQuantizeType.Int8, options.DefaultQuantizeType);
    }

    [Fact]
    public void DefaultOptions_MatchEngineDefaults()
    {
        var options = new ZVecVectorStoreOptions();

        Assert.True(options.EnableMmap);
        Assert.False(options.ReadOnly);
        Assert.Equal(ZVecQuantizeType.Undefined, options.DefaultQuantizeType);
        Assert.Null(options.MemoryLimitMb);
    }
}

/// <summary>
/// Captures the last <see cref="ZVecCollectionOptions"/> passed to <see cref="IZvecFactory.OpenOrCreate"/>.
/// </summary>
internal sealed class CapturingCollectionOptionsFactory : IZvecFactory
{
    private readonly ZVecFactory _inner = new();

    public ZVecCollectionOptions? LastCollectionOptions { get; private set; }

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
        LastCollectionOptions = options;
        return _inner.OpenOrCreate(path, schema, options);
    }

    public ValueTask<IZvecCollection> OpenOrCreateAsync(
        string path,
        ZVecCollectionSchema schema,
        ZVecCollectionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        LastCollectionOptions = options;
        return _inner.OpenOrCreateAsync(path, schema, options, cancellationToken);
    }

    public string GetNativeVersion() => _inner.GetNativeVersion();

    public ZVecNativeAbiInfo GetAbiInfo() => _inner.GetAbiInfo();

    public void Dispose() => _inner.Dispose();

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}

/// <summary>
/// Unit tests verifying collection open passes mmap/read-only options to the native factory.
/// </summary>
public sealed class ZVecCollectionOptionsPlumbingTests : IDisposable
{
    private readonly string _tempPath;
    private readonly CapturingCollectionOptionsFactory _factory;

    public ZVecCollectionOptionsPlumbingTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), "ZVecCollectionOptionsTests", Guid.NewGuid().ToString("N"));
        _factory = new CapturingCollectionOptionsFactory();
    }

    public void Dispose()
    {
        _factory.Dispose();
        if (Directory.Exists(_tempPath))
        {
            try { Directory.Delete(_tempPath, true); } catch { }
        }
    }

    [Fact]
    public async Task OpenNativeCollection_PassesEnableMmap_ToFactory()
    {
        var options = new ZVecVectorStoreOptions
        {
            StoragePath = _tempPath,
            EnableMmap = false,
            ReadOnly = false
        };

        var collection = new ZVecVectorizableRecordCollection<OptimizeTestRecord, string>(
            _factory,
            options,
            "mmap_col");

        await collection.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(_factory.LastCollectionOptions);
        Assert.False(_factory.LastCollectionOptions!.EnableMmap);
        Assert.False(_factory.LastCollectionOptions.ReadOnly);
    }

    [Fact]
    public void ReadOnlyOption_IsStoredOnVectorStoreOptions_ForShippedMobileIndexes()
    {
        var options = new ZVecVectorStoreOptions { ReadOnly = true, EnableMmap = true };
        Assert.True(options.ReadOnly);
        Assert.True(options.EnableMmap);
    }
}
