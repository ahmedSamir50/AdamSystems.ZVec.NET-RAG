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
    /// Source-generated schema with <c>IndexKind = L2</c> must normalize dense scores with the L2
    /// formula through the SG search path (not the Cosine fallback when <c>_typeModel</c> is null).
    /// </summary>
    [Fact]
    public async Task SearchAsync_L2Metric_SourceGeneratedRecord_UsesL2SimilarityFormula()
    {
        string storagePath = CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);
        try
        {
            var options = new ZVecVectorStoreOptions { StoragePath = storagePath };
            IZvecFactory factory = new ZVecFactory();
            factory.Initialize();

            string colName = "l2_score_" + Guid.NewGuid().ToString("N")[..8];
            var collection = new ZVecVectorizableRecordCollection<L2ScoreTestRecord, string>(factory, options, colName);

            var schemaFactory = ZVec.Extensions.VectorData.Mapping.ZVecCollectionSchemaRegistry.Get<L2ScoreTestRecord>();
            Assert.NotNull(schemaFactory);
            var schema = schemaFactory!(colName);
            var indexParam = schema.Vectors.First(v => v.Dimension > 0).IndexParam;
            var hnswParam = Assert.IsType<ZVecHnswIndexParam>(indexParam);
            Assert.Equal(ZVecMetricType.L2, hnswParam.MetricType);

            await collection.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);

            var docVector = new float[768];
            docVector[1] = 1.0f;

            await collection.UpsertAsync(new[]
            {
                new L2ScoreTestRecord { Id = "orthogonal", Title = "orthogonal doc", Vector = docVector }
            }, TestContext.Current.CancellationToken);

            var queryVector = new float[768];
            queryVector[0] = 1.0f;

            VectorSearchResult<L2ScoreTestRecord>? hit = null;
            await foreach (var r in collection.SearchAsync(queryVector, 1, cancellationToken: TestContext.Current.CancellationToken))
            {
                hit = r;
                break;
            }

            Assert.NotNull(hit);
            Assert.NotNull(hit.Score);
            float score = (float)hit.Score.Value;
            const float nativeSquaredL2Distance = 2.0f;
            float expectedL2 = ZVecScoreNormalizer.ToSimilarity(nativeSquaredL2Distance, ZVecMetricType.L2);
            Assert.Equal(expectedL2, score, precision: 5);
            Assert.True(score > 0.3f, "L2 similarity must be > 0.3 for orthogonal unit vectors; Cosine fallback would yield ~0.");
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

/// <summary>L2 metric record for source-generated schema score normalization tests.</summary>
public sealed class L2ScoreTestRecord
{
    [ZVecId]
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [ZVecField]
    [VectorStoreData]
    public string Title { get; set; } = string.Empty;

    [ZVecVector(768)]
    [VectorStoreVector(768, DistanceFunction = DistanceFunction.EuclideanDistance)]
    public ReadOnlyMemory<float> Vector { get; set; }
}
