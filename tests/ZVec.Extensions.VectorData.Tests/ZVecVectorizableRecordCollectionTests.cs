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

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenFactoryIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => 
            new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(null!, TestCollectionName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsArgumentException_WhenCollectionNameInvalid(string? invalidName)
    {
        IZvecFactory factory = new ZVecFactory();
        Assert.Throws<ArgumentException>(() => 
            new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, invalidName!));
    }

    [Fact]
    public void Properties_ReturnExpectedNameAndDefinition_WhenInitialized()
    {
        IZvecFactory factory = new ZVecFactory();
        var customDefinition = new VectorStoreCollectionDefinition();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(
            factory, TestCollectionName, customDefinition);

        Assert.Equal(TestCollectionName, collection.Name);
        Assert.Same(customDefinition, collection.Definition);
    }

    [Fact]
    public async Task GetAsync_ThrowsArgumentNullException_WhenKeyIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);

        await Assert.ThrowsAsync<ArgumentNullException>(() => collection.GetAsync((string)null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetBatchAsync_ThrowsArgumentNullException_WhenKeysEnumerableIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);

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
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);

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
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);

        await Assert.ThrowsAsync<ArgumentNullException>(() => collection.UpsertAsync((SampleCollectionRecord)null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpsertBatchAsync_ThrowsArgumentNullException_WhenRecordsEnumerableIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);

        await Assert.ThrowsAsync<ArgumentNullException>(() => collection.UpsertAsync((IEnumerable<SampleCollectionRecord>)null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsArgumentNullException_WhenKeyIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);

        await Assert.ThrowsAsync<ArgumentNullException>(() => collection.DeleteAsync((string)null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteBatchAsync_ThrowsArgumentNullException_WhenKeysEnumerableIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);

        await Assert.ThrowsAsync<ArgumentNullException>(() => collection.DeleteAsync((IEnumerable<string>)null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchAsync_ThrowsArgumentNullException_WhenSearchValueIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);

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
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);

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
    public async Task SearchAsync_ExecutesPinningPath_WhenVectorIsFloatMemory()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);

        ReadOnlyMemory<float> validVector = new float[768];
        var results = new List<VectorSearchResult<SampleCollectionRecord>>();
        await foreach (var res in collection.SearchAsync(validVector, 10, cancellationToken: TestContext.Current.CancellationToken))
        {
            results.Add(res);
        }

        Assert.Empty(results);
    }

    [Fact]
    public void GetService_ReturnsFactory_WhenRequestedTypeIsIZvecFactory()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);

        object? service = collection.GetService(typeof(IZvecFactory));

        Assert.Same(factory, service);
    }

    [Fact]
    public void GetService_ReturnsNull_WhenRequestedTypeIsUnknown()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);

        object? service = collection.GetService(typeof(int));

        Assert.Null(service);
    }

    [Fact]
    public async Task CollectionLifecycleAsync_MethodsReturnCompletedTasks_WhenInvoked()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);

        bool existsBefore = await collection.CollectionExistsAsync(TestContext.Current.CancellationToken);
        Assert.False(existsBefore);

        await collection.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);
        await collection.EnsureCollectionDeletedAsync(TestContext.Current.CancellationToken);
    }

    // -------------------------------------------------------------------------
    // Cancellation-path tests: every method with ThrowIfCancellationRequested
    // MUST honour a pre-cancelled token. Missing these means the guard clause
    // line is never executed — genuine branch gap.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CollectionExistsAsync_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => collection.CollectionExistsAsync(cts.Token));
    }

    [Fact]
    public async Task EnsureCollectionExistsAsync_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => collection.EnsureCollectionExistsAsync(cts.Token));
    }

    [Fact]
    public async Task EnsureCollectionDeletedAsync_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => collection.EnsureCollectionDeletedAsync(cts.Token));
    }

    [Fact]
    public async Task DeleteAsync_SingleKey_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => collection.DeleteAsync("key-1", cts.Token));
    }

    [Fact]
    public async Task DeleteAsync_BatchKeys_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => collection.DeleteAsync(new[] { "key-1", "key-2" }, cts.Token));
    }

    [Fact]
    public async Task GetAsync_SingleKey_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => collection.GetAsync("key-1", cancellationToken: cts.Token));
    }

    [Fact]
    public async Task GetAsync_BatchKeys_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);
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
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);
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
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);
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
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);
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
        var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName);
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
    // Dictionary<string, object?> TRecord branch: when TRecord == Dictionary,
    // the constructor must NOT call ZVecTypeModel.Get<TRecord>() and must set
    // _typeModel = null. This is the path exercised by GetDynamicCollection.
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WhenTRecordIsDictionary_DoesNotThrow_AndSetsNameCorrectly()
    {
        IZvecFactory factory = new ZVecFactory();
        var definition = new VectorStoreCollectionDefinition();

        // Creating with Dictionary TRecord must NOT call ZVecTypeModel.Get<Dictionary<string,object?>>(),
        // which would throw since Dictionary has no ZVec mapping attributes.
        // If _typeModel is still set (bug), this constructor would throw from ZVecTypeModel.Get.
        var collection = new ZVecVectorizableRecordCollection<Dictionary<string, object?>, object>(
            factory, "dynamic_docs", definition);

        Assert.Equal("dynamic_docs", collection.Name);
        Assert.Same(definition, collection.Definition);
    }
}
