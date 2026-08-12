using Microsoft.Extensions.VectorData;
using ZVec.NET;
using ZVec.NET.Mapping;
using Xunit;

namespace ZVec.Extensions.VectorData.Tests;

/// <summary>
/// Sample record type for Hybrid Search TDD unit tests.
/// </summary>
public sealed class SampleHybridRecord
{
    /// <summary>Document Key.</summary>
    [ZVecId]
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>Text Payload Field.</summary>
    [ZVecField]
    [VectorStoreData(IsIndexed = true, IsFullTextIndexed = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Embedding Vector Field.</summary>
    [ZVecVector(768)]
    [VectorStoreVector(768)]
    public ReadOnlyMemory<float> Vector { get; set; }
}

/// <summary>
/// TDD Unit tests verifying IKeywordHybridSearchable implementation in ZVecVectorizableRecordCollection.
/// </summary>
public sealed class ZVecHybridSearchTests
{
    private static readonly string TestCollectionName = "hybrid_docs";

    [Fact]
    public async Task HybridSearchAsync_ThrowsArgumentNullException_WhenSearchValueIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        IKeywordHybridSearchable<SampleHybridRecord> collection = new ZVecVectorizableRecordCollection<SampleHybridRecord, string>(factory, TestCollectionName);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var item in collection.HybridSearchAsync<string>(
                null!, new[] { "keyword" }, 10, cancellationToken: TestContext.Current.CancellationToken))
            {
                _ = item;
            }
        });
    }

    [Fact]
    public async Task HybridSearchAsync_ThrowsArgumentNullException_WhenKeywordsIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        IKeywordHybridSearchable<SampleHybridRecord> collection = new ZVecVectorizableRecordCollection<SampleHybridRecord, string>(factory, TestCollectionName);

        ReadOnlyMemory<float> vector = new float[768];
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var item in collection.HybridSearchAsync(
                vector, null!, 10, cancellationToken: TestContext.Current.CancellationToken))
            {
                _ = item;
            }
        });
    }

    [Fact]
    public async Task HybridSearchAsync_ThrowsNotSupportedException_WhenSearchValueIsNotFloatMemory()
    {
        IZvecFactory factory = new ZVecFactory();
        IKeywordHybridSearchable<SampleHybridRecord> collection = new ZVecVectorizableRecordCollection<SampleHybridRecord, string>(factory, TestCollectionName);

        double[] invalidVector = new double[768];
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await foreach (var item in collection.HybridSearchAsync(
                invalidVector, new[] { "keyword" }, 10, cancellationToken: TestContext.Current.CancellationToken))
            {
                _ = item;
            }
        });
    }

    [Fact]
    public async Task HybridSearchAsync_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        IKeywordHybridSearchable<SampleHybridRecord> collection = new ZVecVectorizableRecordCollection<SampleHybridRecord, string>(factory, TestCollectionName);

        ReadOnlyMemory<float> vector = new float[768];
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in collection.HybridSearchAsync(
                vector, new[] { "keyword" }, 10, cancellationToken: cts.Token))
            {
                _ = item;
            }
        });
    }

    [Fact]
    public async Task HybridSearchAsync_ExecutesPinningPath_WhenVectorAndKeywordsAreValid()
    {
        IZvecFactory factory = new ZVecFactory();
        IKeywordHybridSearchable<SampleHybridRecord> collection = new ZVecVectorizableRecordCollection<SampleHybridRecord, string>(factory, TestCollectionName);

        ReadOnlyMemory<float> vector = new float[768];
        var keywords = new[] { "AI", "Vector" };

        var enumerable = collection.HybridSearchAsync(vector, keywords, 10, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(enumerable);

        var results = new List<VectorSearchResult<SampleHybridRecord>>();
        await foreach (var res in enumerable)
        {
            results.Add(res);
        }

        // The implementation enters the float memory pinning branch (using var handle = floatMemory.Pin())
        // then hits yield break — result set must be empty for the stub stage.
        Assert.Empty(results);
    }
}
