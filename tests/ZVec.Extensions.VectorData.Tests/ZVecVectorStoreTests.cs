using Microsoft.Extensions.VectorData;
using ZVec.NET;
using ZVec.NET.Mapping;
using Xunit;

namespace ZVec.Extensions.VectorData.Tests;

/// <summary>
/// Sample record type for ZVecVectorStore TDD tests.
/// </summary>
public sealed class TestStoreRecord
{
    /// <summary>Document Key.</summary>
    [ZVecId]
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>Title Data Field.</summary>
    [ZVecField]
    [VectorStoreData]
    public string Title { get; set; } = string.Empty;

    /// <summary>Embedding Vector Field.</summary>
    [ZVecVector(768)]
    [VectorStoreVector(768)]
    public ReadOnlyMemory<float> Vector { get; set; }
}

/// <summary>
/// Unit test suite for ZVecVectorStore (VectorStore implementation).
/// All tests use isolated temp directories — no fake `Assert.False` coincidences.
/// </summary>
public sealed class ZVecVectorStoreTests
{
    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "ZVecTests", Guid.NewGuid().ToString("N"));

    private static ZVecVectorStoreOptions CreateOptions(string? storagePath = null)
        => new() { StoragePath = storagePath ?? CreateTempStoragePath() };

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenZVecFactoryIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ZVecVectorStore(null!, CreateOptions()));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        Assert.Throws<ArgumentNullException>(() => new ZVecVectorStore(factory, null!));
    }

    [Fact]
    public void GetCollection_ReturnsValidCollectionInstance_WhenParametersAreValid()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());

        var collection = store.GetCollection<string, TestStoreRecord>("test_store_records");

        Assert.NotNull(collection);
        Assert.Equal("test_store_records", collection.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetCollection_ThrowsArgumentException_WhenCollectionNameIsNullOrEmpty(string? invalidName)
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());

        Assert.Throws<ArgumentException>(() => store.GetCollection<string, TestStoreRecord>(invalidName!));
    }

    [Fact]
    public void GetCollection_PropagatesDefinition_WhenCustomDefinitionProvided()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());
        var customDefinition = new VectorStoreCollectionDefinition();

        var collection = store.GetCollection<string, TestStoreRecord>("test_store_records", customDefinition);

        Assert.NotNull(collection);
        Assert.Equal("test_store_records", collection.Name);
        var typedCollection = Assert.IsType<ZVecVectorizableRecordCollection<TestStoreRecord, string>>(collection);
        Assert.Same(customDefinition, typedCollection.Definition);
    }

    [Fact]
    public void GetDynamicCollection_ReturnsCollection_WhenParametersAreValid()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());
        var definition = new VectorStoreCollectionDefinition();

        var collection = store.GetDynamicCollection("test_store_records", definition);

        Assert.NotNull(collection);
        Assert.Equal("test_store_records", collection.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetDynamicCollection_ThrowsArgumentException_WhenNameInvalid(string? invalidName)
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());
        var definition = new VectorStoreCollectionDefinition();

        Assert.Throws<ArgumentException>(() => store.GetDynamicCollection(invalidName!, definition));
    }

    /// <summary>
    /// HONEST ROUND-TRIP: EnsureCollectionExistsAsync → CollectionExistsAsync == true →
    /// EnsureCollectionDeletedAsync → CollectionExistsAsync == false.
    /// Replaces the previous "Assert.False(exists)" stub assertion that only passed
    /// because no collection was ever created.
    /// </summary>
    [Fact]
    public async Task CollectionExistsAsync_ReturnsTrue_AfterEnsureCollectionExistsAsync_AndFalse_AfterEnsureCollectionDeletedAsync()
    {
        string storagePath = CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);
        try
        {
            IZvecFactory factory = new ZVecFactory();
            var store = new ZVecVectorStore(factory, CreateOptions(storagePath));
            string collectionName = "lifecycle_" + Guid.NewGuid().ToString("N")[..8];

            // Initially does not exist
            bool existsBefore = await store.CollectionExistsAsync(collectionName, TestContext.Current.CancellationToken);
            Assert.False(existsBefore);

            // After EnsureCollectionExistsAsync, must exist
            var collection = store.GetCollection<string, TestStoreRecord>(collectionName);
            await collection.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);
            bool existsAfterCreate = await store.CollectionExistsAsync(collectionName, TestContext.Current.CancellationToken);
            Assert.True(existsAfterCreate);

            // After EnsureCollectionDeletedAsync on collection (disposes native handle first), must not exist
            await collection.EnsureCollectionDeletedAsync(TestContext.Current.CancellationToken);
            bool existsAfterDelete = await store.CollectionExistsAsync(collectionName, TestContext.Current.CancellationToken);
            Assert.False(existsAfterDelete);
        }
        finally
        {
            if (Directory.Exists(storagePath))
            {
                try { Directory.Delete(storagePath, recursive: true); } catch { }
            }
        }
    }

    /// <summary>
    /// HONEST ROUND-TRIP: ListCollectionNamesAsync returns names of actually-created collections.
    /// Verifies that a created collection appears in enumeration, and that excluded
    /// infrastructure directories (bin/obj/etc.) do NOT appear.
    /// </summary>
    [Fact]
    public async Task ListCollectionNamesAsync_ReturnsCreatedCollection_AndExcludesInfrastructureDirectories()
    {
        string storagePath = CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);
        try
        {
            // Create a "bin" directory to verify exclusion
            Directory.CreateDirectory(Path.Combine(storagePath, "bin"));
            Directory.CreateDirectory(Path.Combine(storagePath, "obj"));

            IZvecFactory factory = new ZVecFactory();
            var store = new ZVecVectorStore(factory, CreateOptions(storagePath));
            string collectionName = "listed_" + Guid.NewGuid().ToString("N")[..8];

            await store.GetCollection<string, TestStoreRecord>(collectionName)
                       .EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);

            var names = new List<string>();
            await foreach (var name in store.ListCollectionNamesAsync(TestContext.Current.CancellationToken))
            {
                names.Add(name);
            }

            Assert.Contains(collectionName, names);
            Assert.DoesNotContain("bin", names);
            Assert.DoesNotContain("obj", names);
        }
        finally
        {
            if (Directory.Exists(storagePath))
            {
                try { Directory.Delete(storagePath, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public void GetService_ReturnsFactory_WhenRequestedTypeIsIZvecFactory()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());

        object? service = store.GetService(typeof(IZvecFactory));

        Assert.Same(factory, service);
    }

    [Fact]
    public void GetService_ReturnsNull_WhenRequestedTypeIsUnknown()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());

        object? service = store.GetService(typeof(string));

        Assert.Null(service);
    }

    [Fact]
    public async Task CollectionExistsAsync_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.CollectionExistsAsync("test_store_records", cts.Token));
    }

    [Fact]
    public async Task EnsureCollectionDeletedAsync_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.EnsureCollectionDeletedAsync("test_store_records", cts.Token));
    }

    [Fact]
    public async Task ListCollectionNamesAsync_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var name in store.ListCollectionNamesAsync(cts.Token))
            {
                _ = name;
            }
        });
    }
}
