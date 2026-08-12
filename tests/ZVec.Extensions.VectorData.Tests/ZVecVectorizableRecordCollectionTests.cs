using System.Linq.Expressions;
using Microsoft.Extensions.VectorData;
using ZVec.NET;
using ZVec.NET.Mapping;
using Xunit;

namespace ZVec.Extensions.VectorData.Tests;

/// <summary>
/// Sample record POCO for collection CRUD and vectorized search tests.
/// </summary>
public sealed class SampleCollectionRecord
{
    /// <summary>Document Key.</summary>
    [ZVecId]
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>Document Title Payload.</summary>
    [ZVecField]
    [VectorStoreData(IsIndexed = true, IsFullTextIndexed = true)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Vector Embedding.</summary>
    [ZVecVector(768)]
    [VectorStoreVector(768)]
    public ReadOnlyMemory<float> Vector { get; set; }
}

/// <summary>
/// TDD Unit test suite for ZVecVectorizableRecordCollection (VectorStoreCollection implementation).
/// </summary>
public sealed class ZVecVectorizableRecordCollectionTests
{
    private static readonly string TestCollectionName = "docs_collection";

    private static ZVecVectorStoreOptions CreateOptions(string? storagePath = null)
        => new() { StoragePath = storagePath ?? Path.Combine(Path.GetTempPath(), "ZVecTests", Guid.NewGuid().ToString("N")) };

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenFactoryIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(null!, CreateOptions(), TestCollectionName));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        Assert.Throws<ArgumentNullException>(() =>
            new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, null!, TestCollectionName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsArgumentException_WhenCollectionNameInvalid(string? invalidName)
    {
        IZvecFactory factory = new ZVecFactory();
        Assert.Throws<ArgumentException>(() =>
            new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), invalidName!));
    }

    [Fact]
    public void Properties_ReturnExpectedNameAndDefinition_WhenInitialized()
    {
        IZvecFactory factory = new ZVecFactory();
        var customDefinition = new VectorStoreCollectionDefinition();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(
            factory, CreateOptions(), TestCollectionName, customDefinition);

        Assert.Equal(TestCollectionName, collection.Name);
        Assert.Same(customDefinition, collection.Definition);
    }

    [Fact]
    public async Task GetAsync_ThrowsArgumentNullException_WhenKeyIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);

        await Assert.ThrowsAsync<ArgumentNullException>(() => collection.GetAsync((string)null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetBatchAsync_ThrowsArgumentNullException_WhenKeysEnumerableIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var item in collection.GetAsync((IEnumerable<string>)null!, cancellationToken: TestContext.Current.CancellationToken))
            {
                _ = item;
            }
        });
    }

    [Fact]
    public async Task GetAsync_Filter_ThrowsArgumentNullException_WhenFilterIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var item in collection.GetAsync((Expression<Func<SampleCollectionRecord, bool>>)null!, 10, cancellationToken: TestContext.Current.CancellationToken))
            {
                _ = item;
            }
        });
    }

    [Fact]
    public async Task UpsertAsync_ThrowsArgumentNullException_WhenRecordIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);

        await Assert.ThrowsAsync<ArgumentNullException>(() => collection.UpsertAsync((SampleCollectionRecord)null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpsertBatchAsync_ThrowsArgumentNullException_WhenRecordsEnumerableIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);

        await Assert.ThrowsAsync<ArgumentNullException>(() => collection.UpsertAsync((IEnumerable<SampleCollectionRecord>)null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsArgumentNullException_WhenKeyIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);

        await Assert.ThrowsAsync<ArgumentNullException>(() => collection.DeleteAsync((string)null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteBatchAsync_ThrowsArgumentNullException_WhenKeysEnumerableIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);

        await Assert.ThrowsAsync<ArgumentNullException>(() => collection.DeleteAsync((IEnumerable<string>)null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchAsync_ThrowsArgumentNullException_WhenSearchValueIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var res in collection.SearchAsync<string>(null!, 10, cancellationToken: TestContext.Current.CancellationToken))
            {
                _ = res;
            }
        });
    }

    [Fact]
    public async Task SearchAsync_ThrowsNotSupportedException_WhenVectorTypeIsNotReadOnlyMemoryOfFloat()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);

        double[] unsupportedVector = new double[768];
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await foreach (var res in collection.SearchAsync(unsupportedVector, 10, cancellationToken: TestContext.Current.CancellationToken))
            {
                _ = res;
            }
        });
    }

    [Fact]
    public async Task UpsertAndGet_RoundTrip_ReturnsUpsertedRecord_AndSearchFindsIt()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ZVecTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = new ZVecVectorStoreOptions { StoragePath = tempDir };
            IZvecFactory factory = new ZVecFactory();
            factory.Initialize();

            string colName = "test_roundtrip_" + Guid.NewGuid().ToString("N")[..8];
            var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, options, colName);

            await collection.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);
            bool exists = await collection.CollectionExistsAsync(TestContext.Current.CancellationToken);
            Assert.True(exists);

            // Verify collection files actually live in tempDir, not in bin/
            string expectedCollectionPath = Path.Combine(tempDir, colName);
            Assert.True(Directory.Exists(expectedCollectionPath),
                $"Collection directory should exist at {expectedCollectionPath}, not in bin/.");

            var floatArray = new float[768];
            floatArray[0] = 1.0f;
            floatArray[1] = 0.5f;

            var record = new SampleCollectionRecord
            {
                Id = "doc1",
                Title = "TDD Real Vector Search Doc",
                Vector = floatArray
            };

            await collection.UpsertAsync(record, TestContext.Current.CancellationToken);

            var retrieved = await collection.GetAsync("doc1", cancellationToken: TestContext.Current.CancellationToken);
            Assert.NotNull(retrieved);
            Assert.Equal("doc1", retrieved.Id);
            Assert.Equal("TDD Real Vector Search Doc", retrieved.Title);

            var searchResults = new List<VectorSearchResult<SampleCollectionRecord>>();
            await foreach (var res in collection.SearchAsync(record.Vector, 10, cancellationToken: TestContext.Current.CancellationToken))
            {
                searchResults.Add(res);
            }

            Assert.NotEmpty(searchResults);
            Assert.Equal("doc1", searchResults[0].Record.Id);

            await collection.DeleteAsync("doc1", TestContext.Current.CancellationToken);
            var deletedDoc = await collection.GetAsync("doc1", cancellationToken: TestContext.Current.CancellationToken);
            Assert.Null(deletedDoc);

            await collection.EnsureCollectionDeletedAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public void GetService_ReturnsFactory_WhenRequestedTypeIsIZvecFactory()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);

        object? service = collection.GetService(typeof(IZvecFactory));

        Assert.Same(factory, service);
    }

    [Fact]
    public void GetService_ReturnsNull_WhenRequestedTypeIsUnknown()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);

        object? service = collection.GetService(typeof(int));

        Assert.Null(service);
    }

    // -------------------------------------------------------------------------
    // Cancellation-path tests: every method with ThrowIfCancellationRequested
    // MUST honour a pre-cancelled token.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CollectionExistsAsync_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => collection.CollectionExistsAsync(cts.Token));
    }

    [Fact]
    public async Task EnsureCollectionExistsAsync_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => collection.EnsureCollectionExistsAsync(cts.Token));
    }

    [Fact]
    public async Task EnsureCollectionDeletedAsync_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => collection.EnsureCollectionDeletedAsync(cts.Token));
    }

    [Fact]
    public async Task DeleteAsync_SingleKey_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => collection.DeleteAsync("key-1", cts.Token));
    }

    [Fact]
    public async Task DeleteAsync_BatchKeys_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => collection.DeleteAsync(new[] { "key-1", "key-2" }, cts.Token));
    }

    [Fact]
    public async Task GetAsync_SingleKey_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => collection.GetAsync("key-1", cancellationToken: cts.Token));
    }

    [Fact]
    public async Task GetAsync_BatchKeys_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in collection.GetAsync(new[] { "key-1" }, cancellationToken: cts.Token))
            {
                _ = item;
            }
        });
    }

    [Fact]
    public async Task GetAsync_Filter_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Expression<Func<SampleCollectionRecord, bool>> filter = x => x.Title == "test";
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in collection.GetAsync(filter, 10, cancellationToken: cts.Token))
            {
                _ = item;
            }
        });
    }

    [Fact]
    public async Task UpsertAsync_SingleRecord_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var record = new SampleCollectionRecord { Id = "r1", Title = "Doc" };
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => collection.UpsertAsync(record, cts.Token));
    }

    [Fact]
    public async Task UpsertAsync_BatchRecords_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var records = new[] { new SampleCollectionRecord { Id = "r1", Title = "Doc" } };
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => collection.UpsertAsync(records, cts.Token));
    }

    [Fact]
    public async Task SearchAsync_FloatMemoryPath_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, CreateOptions(), TestCollectionName);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        ReadOnlyMemory<float> vector = new float[768];
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var res in collection.SearchAsync(vector, 10, cancellationToken: cts.Token))
            {
                _ = res;
            }
        });
    }

    // -------------------------------------------------------------------------
    // Dictionary<string, object?> TRecord branch
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WhenTRecordIsDictionary_DoesNotThrow_AndSetsNameCorrectly()
    {
        IZvecFactory factory = new ZVecFactory();
        var definition = new VectorStoreCollectionDefinition();

        var collection = new ZVecVectorizableRecordCollection<Dictionary<string, object?>, object>(
            factory, CreateOptions(), "dynamic_docs", definition);

        Assert.Equal("dynamic_docs", collection.Name);
        Assert.Same(definition, collection.Definition);
    }
}
