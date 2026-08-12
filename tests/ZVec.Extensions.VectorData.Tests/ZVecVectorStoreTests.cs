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
/// </summary>
public sealed class ZVecVectorStoreTests
{
    private static readonly string TestCollectionName = "test_store_records";

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenZVecFactoryIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ZVecVectorStore(null!));
    }

    [Fact]
    public void GetCollection_ReturnsValidCollectionInstance_WhenParametersAreValid()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory);

        var collection = store.GetCollection<string, TestStoreRecord>(TestCollectionName);

        Assert.NotNull(collection);
        Assert.Equal(TestCollectionName, collection.Name);
    }

    [Fact]
    public void GetCollection_ThrowsArgumentException_WhenCollectionNameIsNullOrEmpty()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory);

        Assert.Throws<ArgumentException>(() => store.GetCollection<string, TestStoreRecord>(null!));
        Assert.Throws<ArgumentException>(() => store.GetCollection<string, TestStoreRecord>(string.Empty));
        Assert.Throws<ArgumentException>(() => store.GetCollection<string, TestStoreRecord>("   "));
    }

    [Fact]
    public void GetCollection_PropagatesDefinition_WhenCustomDefinitionProvided()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory);
        var customDefinition = new VectorStoreCollectionDefinition();

        var collection = store.GetCollection<string, TestStoreRecord>(TestCollectionName, customDefinition);

        Assert.NotNull(collection);
        Assert.Equal(TestCollectionName, collection.Name);
        // Verify the definition is propagated through to the underlying collection.
        var typedCollection = Assert.IsType<ZVecVectorizableRecordCollection<TestStoreRecord, string>>(collection);
        Assert.Same(customDefinition, typedCollection.Definition);
    }

    [Fact]
    public void GetDynamicCollection_ReturnsCollection_WhenParametersAreValid()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory);
        var definition = new VectorStoreCollectionDefinition();

        var collection = store.GetDynamicCollection(TestCollectionName, definition);

        Assert.NotNull(collection);
        Assert.Equal(TestCollectionName, collection.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetDynamicCollection_ThrowsArgumentException_WhenNameInvalid(string? invalidName)
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory);
        var definition = new VectorStoreCollectionDefinition();

        Assert.Throws<ArgumentException>(() => store.GetDynamicCollection(invalidName!, definition));
    }

    [Fact]
    public async Task CollectionExistsAsync_ReturnsFalse_WhenInvoked()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory);

        bool exists = await store.CollectionExistsAsync(TestCollectionName, TestContext.Current.CancellationToken);

        Assert.False(exists);
    }

    [Fact]
    public async Task EnsureCollectionDeletedAsync_CompletesSuccessfully_WhenInvoked()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory);

        await store.EnsureCollectionDeletedAsync(TestCollectionName, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void GetService_ReturnsFactory_WhenRequestedTypeIsIZvecFactory()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory);

        object? service = store.GetService(typeof(IZvecFactory));

        Assert.Same(factory, service);
    }

    [Fact]
    public void GetService_ReturnsNull_WhenRequestedTypeIsUnknown()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory);

        object? service = store.GetService(typeof(string));

        Assert.Null(service);
    }

    [Fact]
    public async Task ListCollectionNamesAsync_EnumeratesCreatedCollections()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory);

        var names = new List<string>();
        await foreach (var name in store.ListCollectionNamesAsync(TestContext.Current.CancellationToken))
        {
            names.Add(name);
        }

        Assert.NotNull(names);
    }

    [Fact]
    public async Task CollectionExistsAsync_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.CollectionExistsAsync(TestCollectionName, cts.Token));
    }

    [Fact]
    public async Task EnsureCollectionDeletedAsync_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.EnsureCollectionDeletedAsync(TestCollectionName, cts.Token));
    }

    [Fact]
    public async Task ListCollectionNamesAsync_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory);
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
