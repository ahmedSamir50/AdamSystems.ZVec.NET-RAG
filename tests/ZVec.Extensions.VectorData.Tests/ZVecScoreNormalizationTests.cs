using Microsoft.Extensions.VectorData;
using ZVec.NET;
using ZVec.NET.Mapping;
using Xunit;

namespace ZVec.Extensions.VectorData.Tests;

/// <summary>
/// TDD unit tests for score normalization in ZVecVectorizableRecordCollection.
/// Verifies the formula matrix documented in docs/architecture/score-semantics.md:
///   Cosine:       similarity = 1.0f - distance
///   L2:           similarity = 1.0f / (1.0f + distance)
///   InnerProduct: similarity = nativeScore (passthrough)
/// </summary>
public sealed class ZVecScoreNormalizationTests
{
    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "ZVecTests", Guid.NewGuid().ToString("N"));

    private sealed class ScoreTestRecord
    {
        [ZVecId]
        [VectorStoreKey]
        public string Id { get; set; } = string.Empty;

        [ZVecField]
        [VectorStoreData]
        public string Title { get; set; } = string.Empty;

        [ZVecVector(768)]
        [VectorStoreVector(768)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }

    /// <summary>
    /// HIGHER DISTANCE = LOWER SIMILARITY.
    /// Two records with cosine distances 0.1 and 0.5 must produce scores where
    /// the 0.1-distance record has the higher similarity score.
    /// </summary>
    [Fact]
    public async Task SearchAsync_CosineMetric_HigherDistance_ProducesLowerSimilarityScore()
    {
        string storagePath = CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);
        try
        {
            var options = new ZVecVectorStoreOptions { StoragePath = storagePath };
            IZvecFactory factory = new ZVecFactory();
            factory.Initialize();

            string colName = "score_norm_" + Guid.NewGuid().ToString("N")[..8];
            var collection = new ZVecVectorizableRecordCollection<ScoreTestRecord, string>(factory, options, colName);
            await collection.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);

            // Seed two records with orthogonal vectors
            var nearVector = new float[768]; nearVector[0] = 1.0f;
            var farVector = new float[768]; farVector[1] = 1.0f;

            await collection.UpsertAsync(new[]
            {
                new ScoreTestRecord { Id = "near", Title = "near doc", Vector = nearVector },
                new ScoreTestRecord { Id = "far", Title = "far doc", Vector = farVector }
            }, TestContext.Current.CancellationToken);

            // Query with the near vector — "near" record should score higher than "far"
            var queryVector = new float[768]; queryVector[0] = 1.0f;
            var results = new List<VectorSearchResult<ScoreTestRecord>>();
            await foreach (var r in collection.SearchAsync(queryVector, 10, cancellationToken: TestContext.Current.CancellationToken))
            {
                results.Add(r);
            }

            Assert.True(results.Count >= 2, "Search must return at least 2 results to compare scores.");

            var nearResult = results.Find(r => r.Record.Id == "near");
            var farResult = results.Find(r => r.Record.Id == "far");

            Assert.NotNull(nearResult);
            Assert.NotNull(farResult);
            Assert.True(nearResult!.Score > farResult!.Score,
                $"Near record score ({nearResult.Score}) must be greater than far record score ({farResult.Score}). " +
                "If this fails, score normalization formula is wrong — see docs/architecture/score-semantics.md.");

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

    /// <summary>
    /// SCORE ORDERING: OrderByDescending(Score) must return best matches first.
    /// This is the core contract for downstream RAG rankers.
    /// </summary>
    [Fact]
    public async Task SearchAsync_Results_AreOrderedByScoreDescending()
    {
        string storagePath = CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);
        try
        {
            var options = new ZVecVectorStoreOptions { StoragePath = storagePath };
            IZvecFactory factory = new ZVecFactory();
            factory.Initialize();

            string colName = "score_order_" + Guid.NewGuid().ToString("N")[..8];
            var collection = new ZVecVectorizableRecordCollection<ScoreTestRecord, string>(factory, options, colName);
            await collection.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);

            var records = new List<ScoreTestRecord>();
            for (int i = 0; i < 5; i++)
            {
                var v = new float[768];
                v[0] = (5 - i) / 5.0f;  // decreasing similarity to query
                records.Add(new ScoreTestRecord { Id = $"doc{i}", Title = $"doc{i}", Vector = v });
            }
            await collection.UpsertAsync(records, TestContext.Current.CancellationToken);

            var queryVector = new float[768]; queryVector[0] = 1.0f;
            var results = new List<VectorSearchResult<ScoreTestRecord>>();
            await foreach (var r in collection.SearchAsync(queryVector, 5, cancellationToken: TestContext.Current.CancellationToken))
            {
                results.Add(r);
            }

            // Assert descending order
            for (int i = 1; i < results.Count; i++)
            {
                Assert.True(results[i - 1].Score >= results[i].Score,
                    $"Results must be in descending score order. Position {i - 1} score {results[i - 1].Score} < position {i} score {results[i].Score}.");
            }

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
