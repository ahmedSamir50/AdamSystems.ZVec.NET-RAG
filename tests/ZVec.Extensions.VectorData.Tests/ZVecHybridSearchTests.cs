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
/// All tests use isolated temp directories and round-trip real data — no stubs.
/// </summary>
public sealed class ZVecHybridSearchTests
{
    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "ZVecTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task HybridSearchAsync_ThrowsArgumentNullException_WhenSearchValueIsNull()
    {
        var options = new ZVecVectorStoreOptions { StoragePath = CreateTempStoragePath() };
        IZvecFactory factory = new ZVecFactory();
        IKeywordHybridSearchable<SampleHybridRecord> collection =
            new ZVecVectorizableRecordCollection<SampleHybridRecord, string>(factory, options, "hybrid_docs");

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
        var options = new ZVecVectorStoreOptions { StoragePath = CreateTempStoragePath() };
        IZvecFactory factory = new ZVecFactory();
        IKeywordHybridSearchable<SampleHybridRecord> collection =
            new ZVecVectorizableRecordCollection<SampleHybridRecord, string>(factory, options, "hybrid_docs");

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
        var options = new ZVecVectorStoreOptions { StoragePath = CreateTempStoragePath() };
        IZvecFactory factory = new ZVecFactory();
        IKeywordHybridSearchable<SampleHybridRecord> collection =
            new ZVecVectorizableRecordCollection<SampleHybridRecord, string>(factory, options, "hybrid_docs");

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
        var options = new ZVecVectorStoreOptions { StoragePath = CreateTempStoragePath() };
        IZvecFactory factory = new ZVecFactory();
        IKeywordHybridSearchable<SampleHybridRecord> collection =
            new ZVecVectorizableRecordCollection<SampleHybridRecord, string>(factory, options, "hybrid_docs");

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

    /// <summary>
    /// HONEST HYBRID ROUND-TRIP TEST:
    /// Seeds real records with FTS-indexed text, executes HybridSearchAsync with vector + keywords,
    /// asserts non-empty results, and verifies that the record matching the keyword is returned.
    /// This test replaces the previous "Assert.Empty(results)" stub-state assertion.
    /// </summary>
    [Fact]
    public async Task HybridSearchAsync_ReturnsNonEmptyResults_WhenSeededWithRealRecordsAndKeywords()
    {
        string storagePath = CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);
        try
        {
            var options = new ZVecVectorStoreOptions { StoragePath = storagePath };
            IZvecFactory factory = new ZVecFactory();
            factory.Initialize();

            string colName = "hybrid_test_" + Guid.NewGuid().ToString("N")[..8];
            var collection = new ZVecVectorizableRecordCollection<SampleHybridRecord, string>(factory, options, colName);
            await collection.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);

            // Seed records with distinct content
            var vector1 = new float[768]; vector1[0] = 1.0f;
            var vector2 = new float[768]; vector2[0] = 0.8f;
            var vector3 = new float[768]; vector3[0] = 0.6f;

            await collection.UpsertAsync(new[]
            {
                new SampleHybridRecord { Id = "doc1", Content = "machine learning vector embeddings", Vector = vector1 },
                new SampleHybridRecord { Id = "doc2", Content = "neural network architecture", Vector = vector2 },
                new SampleHybridRecord { Id = "doc3", Content = "document retrieval keyword search", Vector = vector3 }
            }, TestContext.Current.CancellationToken);

            // Execute hybrid search with vector + keyword that matches doc3's content
            var queryVector = new float[768]; queryVector[0] = 0.7f;
            var keywords = new[] { "keyword", "retrieval" };

            var results = new List<VectorSearchResult<SampleHybridRecord>>();
            IKeywordHybridSearchable<SampleHybridRecord> hybrid = collection;
            await foreach (var res in hybrid.HybridSearchAsync(
                queryVector, keywords, 10, cancellationToken: TestContext.Current.CancellationToken))
            {
                results.Add(res);
            }

            // Assert non-empty results — implementation must actually execute the query
            Assert.NotEmpty(results);

            // Assert the keyword-matching document appears in results
            Assert.Contains(results, r => r.Record.Id == "doc3");

            await collection.EnsureCollectionDeletedAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            if (Directory.Exists(storagePath))
            {
                try { Directory.Delete(storagePath, recursive: true); } catch { }
            }
        }
    }
}
