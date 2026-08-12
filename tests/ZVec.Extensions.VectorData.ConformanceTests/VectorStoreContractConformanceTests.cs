using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData.Attributes;
using ZVec.NET;
using ZVec.NET.Mapping;
using Xunit;

namespace ZVec.Extensions.VectorData.ConformanceTests;

/// <summary>
/// Sample record class for VectorStore contract conformance tests.
/// Decorated with both Microsoft VectorStore attributes and ZVec native mapping attributes.
/// </summary>
public sealed class ConformanceRecord
{
    [ZVecId]
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [ZVecField]
    [VectorStoreData(IsIndexed = true, IsFullTextIndexed = true)]
    [ZVecFullTextSearch]
    public string Title { get; set; } = string.Empty;

    [ZVecField]
    [VectorStoreData]
    public int CategoryId { get; set; }

    [ZVecVector(4)]
    [VectorStoreVector(4)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}

/// <summary>
/// Full contract conformance test suite for Microsoft.Extensions.VectorData implementation (ZVecVectorStore &amp; ZVecVectorizableRecordCollection).
/// Verifies contract compliance for IVectorStore, VectorStoreCollection, IVectorizedSearch, and IKeywordHybridSearchable.
/// </summary>
public sealed class VectorStoreContractConformanceTests : IDisposable
{
    private readonly string _tempPath;
    private readonly ZVecVectorStoreOptions _options;
    private readonly ZVecFactory _factory;
    private readonly ZVecVectorStore _store;

    public VectorStoreContractConformanceTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), "ZVecConformance", Guid.NewGuid().ToString("N"));
        _options = new ZVecVectorStoreOptions { StoragePath = _tempPath };
        _factory = new ZVecFactory();
        _store = new ZVecVectorStore(_factory, _options);
    }

    public void Dispose()
    {
        _factory.Dispose();
        if (Directory.Exists(_tempPath))
        {
            try { Directory.Delete(_tempPath, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task IVectorStore_LifecycleContract_CollectionManagement()
    {
        var ct = TestContext.Current.CancellationToken;
        string collectionName = "conformance_lifecycle_col";
        var collection = _store.GetCollection<string, ConformanceRecord>(collectionName);

        Assert.NotNull(collection);
        Assert.Equal(collectionName, collection.Name);

        // 1. Check initially non-existent
        bool existsInitial = await collection.CollectionExistsAsync(ct);
        Assert.False(existsInitial);

        // 2. Ensure collection exists
        await collection.EnsureCollectionExistsAsync(ct);
        bool existsAfterEnsure = await collection.CollectionExistsAsync(ct);
        Assert.True(existsAfterEnsure);

        // 3. List collection names
        var names = new List<string>();
        await foreach (var name in _store.ListCollectionNamesAsync(ct))
        {
            names.Add(name);
        }
        Assert.Contains(collectionName, names);

        // 4. Ensure collection deleted
        await collection.EnsureCollectionDeletedAsync(ct);
        bool existsAfterDelete = await collection.CollectionExistsAsync(ct);
        Assert.False(existsAfterDelete);
    }

    [Fact]
    public async Task VectorStoreCollection_CRUDContract_SingleAndBatchOperations()
    {
        var ct = TestContext.Current.CancellationToken;
        var collection = _store.GetCollection<string, ConformanceRecord>("conformance_crud_col");
        await collection.EnsureCollectionExistsAsync(ct);

        var rec1 = new ConformanceRecord { Id = "1", Title = "Doc 1", CategoryId = 10, Embedding = new float[] { 1f, 0f, 0f, 0f } };
        var rec2 = new ConformanceRecord { Id = "2", Title = "Doc 2", CategoryId = 20, Embedding = new float[] { 0f, 1f, 0f, 0f } };
        var rec3 = new ConformanceRecord { Id = "3", Title = "Doc 3", CategoryId = 30, Embedding = new float[] { 0f, 0f, 1f, 0f } };

        // 1. Single Upsert & Get
        await collection.UpsertAsync(rec1, ct);
        var fetched1 = await collection.GetAsync("1", cancellationToken: ct);
        Assert.NotNull(fetched1);
        Assert.Equal("Doc 1", fetched1.Title);
        Assert.Equal(10, fetched1.CategoryId);

        // 2. Batch Upsert & Batch Get
        await collection.UpsertAsync(new[] { rec2, rec3 }, ct);
        var batchFetched = new List<ConformanceRecord>();
        await foreach (var item in collection.GetAsync(new[] { "2", "3" }, cancellationToken: ct))
        {
            batchFetched.Add(item);
        }
        Assert.Equal(2, batchFetched.Count);
        Assert.Contains(batchFetched, r => r.Id == "2" && r.Title == "Doc 2");
        Assert.Contains(batchFetched, r => r.Id == "3" && r.Title == "Doc 3");

        // 3. Single Delete
        await collection.DeleteAsync("1", ct);
        var fetchedDeleted = await collection.GetAsync("1", cancellationToken: ct);
        Assert.Null(fetchedDeleted);

        // 4. Batch Delete
        await collection.DeleteAsync(new[] { "2", "3" }, ct);
        var batchFetchedDeleted = new List<ConformanceRecord>();
        await foreach (var item in collection.GetAsync(new[] { "2", "3" }, cancellationToken: ct))
        {
            batchFetchedDeleted.Add(item);
        }
        Assert.Empty(batchFetchedDeleted);
    }

    [Fact]
    public async Task IVectorizedSearch_Contract_VectorSearchReturnsNormalizedScores()
    {
        var ct = TestContext.Current.CancellationToken;
        var collection = _store.GetCollection<string, ConformanceRecord>("conformance_search_col");
        await collection.EnsureCollectionExistsAsync(ct);

        var rec1 = new ConformanceRecord { Id = "1", Title = "Target Alpha", CategoryId = 1, Embedding = new float[] { 1f, 0f, 0f, 0f } };
        var rec2 = new ConformanceRecord { Id = "2", Title = "Target Beta", CategoryId = 2, Embedding = new float[] { 0f, 1f, 0f, 0f } };
        await collection.UpsertAsync(new[] { rec1, rec2 }, ct);

        ReadOnlyMemory<float> queryVector = new float[] { 1f, 0f, 0f, 0f };
        var results = new List<VectorSearchResult<ConformanceRecord>>();

        await foreach (var result in collection.SearchAsync(queryVector, top: 2, cancellationToken: ct))
        {
            results.Add(result);
        }

        Assert.NotEmpty(results);
        Assert.Equal("1", results[0].Record.Id);
        Assert.NotNull(results[0].Score);
        // Cosine distance = 0 => similarity score = 1.0
        Assert.True(results[0].Score >= 0.99f, $"Expected score ~1.0 but got {results[0].Score}");
    }

    [Fact]
    public async Task IKeywordHybridSearchable_Contract_HybridSearchExecution()
    {
        var ct = TestContext.Current.CancellationToken;
        IKeywordHybridSearchable<ConformanceRecord> collection =
            (ZVecVectorizableRecordCollection<ConformanceRecord, string>)_store.GetCollection<string, ConformanceRecord>("conformance_hybrid_col");

        var recordCollection = (VectorStoreCollection<string, ConformanceRecord>)collection;
        await recordCollection.EnsureCollectionExistsAsync(ct);

        var rec1 = new ConformanceRecord { Id = "1", Title = "Architecture Deep Dive", CategoryId = 1, Embedding = new float[] { 1f, 0f, 0f, 0f } };
        var rec2 = new ConformanceRecord { Id = "2", Title = "Performance Benchmarks", CategoryId = 2, Embedding = new float[] { 0f, 1f, 0f, 0f } };
        await recordCollection.UpsertAsync(new[] { rec1, rec2 }, ct);

        var results = new List<VectorSearchResult<ConformanceRecord>>();
        ReadOnlyMemory<float> dummyVector = new float[] { 1f, 0f, 0f, 0f };
        await foreach (var result in collection.HybridSearchAsync(dummyVector, new[] { "Architecture" }, top: 2, cancellationToken: ct))
        {
            results.Add(result);
        }

        Assert.NotEmpty(results);
        Assert.Equal("1", results[0].Record.Id);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenKeyDoesNotExist()
    {
        var ct = TestContext.Current.CancellationToken;
        var collection = _store.GetCollection<string, ConformanceRecord>("conformance_missing_key_col");
        await collection.EnsureCollectionExistsAsync(ct);

        var fetched = await collection.GetAsync("does-not-exist", cancellationToken: ct);

        Assert.Null(fetched);
    }

    [Fact]
    public async Task SearchAsync_ThrowsArgumentNullException_WhenSearchValueIsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var collection = _store.GetCollection<string, ConformanceRecord>("conformance_null_search_col");
        await collection.EnsureCollectionExistsAsync(ct);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in collection.SearchAsync<float[]>(null!, top: 1, cancellationToken: ct))
            {
            }
        });
    }

    [Fact]
    public async Task EmptyCollection_SearchReturnsNoResults_AndGetReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var collection = _store.GetCollection<string, ConformanceRecord>("conformance_empty_col");
        await collection.EnsureCollectionExistsAsync(ct);

        var missing = await collection.GetAsync("missing", cancellationToken: ct);
        Assert.Null(missing);

        var results = new List<VectorSearchResult<ConformanceRecord>>();
        await foreach (var result in collection.SearchAsync(new float[] { 1f, 0f, 0f, 0f }, top: 5, cancellationToken: ct))
        {
            results.Add(result);
        }

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetBatchAsync_ReturnsOnlyExistingRecords_WhenKeysAreMixed()
    {
        var ct = TestContext.Current.CancellationToken;
        var collection = _store.GetCollection<string, ConformanceRecord>("conformance_mixed_keys_col");
        await collection.EnsureCollectionExistsAsync(ct);

        await collection.UpsertAsync(
            new ConformanceRecord { Id = "exists", Title = "Present", CategoryId = 1, Embedding = new float[] { 1f, 0f, 0f, 0f } },
            ct);

        var fetched = new List<ConformanceRecord>();
        await foreach (var item in collection.GetAsync(new[] { "exists", "missing" }, cancellationToken: ct))
        {
            fetched.Add(item);
        }

        Assert.Single(fetched);
        Assert.Equal("exists", fetched[0].Id);
    }

    [Fact]
    public async Task EnsureCollectionDeletedAsync_RemovesCollection_AndSubsequentExistsCheckReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        var collection = _store.GetCollection<string, ConformanceRecord>("conformance_deleted_col");
        await collection.EnsureCollectionExistsAsync(ct);
        Assert.True(await collection.CollectionExistsAsync(ct));

        await collection.EnsureCollectionDeletedAsync(ct);

        Assert.False(await collection.CollectionExistsAsync(ct));
    }

    [Fact]
    public async Task ZVecFullTextSearchAttribute_EnablesHybridSearch_WithoutVectorStoreDataFullTextFlag()
    {
        var ct = TestContext.Current.CancellationToken;
        IKeywordHybridSearchable<FtsOnlyRecord> collection =
            (ZVecVectorizableRecordCollection<FtsOnlyRecord, string>)_store.GetCollection<string, FtsOnlyRecord>("conformance_fts_only_col");

        var recordCollection = (VectorStoreCollection<string, FtsOnlyRecord>)collection;
        await recordCollection.EnsureCollectionExistsAsync(ct);

        await recordCollection.UpsertAsync(
            new FtsOnlyRecord { Id = "1", Body = "Vector database architecture", Embedding = new float[] { 1f, 0f, 0f, 0f } },
            ct);

        var results = new List<VectorSearchResult<FtsOnlyRecord>>();
        await foreach (var result in collection.HybridSearchAsync(
            new float[] { 1f, 0f, 0f, 0f },
            new[] { "architecture" },
            top: 1,
            cancellationToken: ct))
        {
            results.Add(result);
        }

        Assert.NotEmpty(results);
        Assert.Equal("1", results[0].Record.Id);
    }
}

/// <summary>
/// Record that enables FTS via <see cref="ZVecFullTextSearchAttribute"/> only.
/// </summary>
public sealed class FtsOnlyRecord
{
    [ZVecId]
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [ZVecField]
    [VectorStoreData]
    [ZVecFullTextSearch]
    public string Body { get; set; } = string.Empty;

    [ZVecVector(4)]
    [VectorStoreVector(4)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
