using Microsoft.Extensions.VectorData;
using ZVec.NET;
using Xunit;

namespace ZVec.Extensions.VectorData.Tests;

/// <summary>
/// Record decorated with VectorStore attributes only (no ZVec mapping attributes).
/// </summary>
public sealed class VectorDataOnlyRecord
{
    /// <summary>Document key.</summary>
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>Indexed text payload.</summary>
    [VectorStoreData(IsIndexed = true, IsFullTextIndexed = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Scalar filter field (not FTS).</summary>
    [VectorStoreData(IsIndexed = true)]
    public string Category { get; set; } = string.Empty;

    /// <summary>Dense embedding.</summary>
    [VectorStoreVector(4)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}

/// <summary>
/// Plain POCO without VectorStore attributes — used to verify explicit definition schema mapping.
/// </summary>
public sealed class DefinitionOnlyRecord
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public ReadOnlyMemory<float> Embedding { get; set; }
}

/// <summary>
/// Verifies source-generated schema and mapper integration for VectorData-only POCOs.
/// </summary>
public sealed class ZVecSourceGeneratedSchemaTests
{
    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "ZVecTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task VectorDataOnlyRecord_UpsertAndGet_RoundTripsUsingGeneratedMapper()
    {
        string storagePath = CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);
        try
        {
            var options = new ZVecVectorStoreOptions { StoragePath = storagePath };
            IZvecFactory factory = new ZVecFactory();
            factory.Initialize();

            var collection = new ZVecVectorizableRecordCollection<VectorDataOnlyRecord, string>(
                factory,
                options,
                "vector_data_only_" + Guid.NewGuid().ToString("N")[..8]);

            await collection.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);

            var vector = new float[] { 1f, 0f, 0f, 0f };
            var record = new VectorDataOnlyRecord
            {
                Id = "only-vector-data",
                Content = "generated schema path",
                Embedding = vector
            };

            await collection.UpsertAsync(record, TestContext.Current.CancellationToken);
            var fetched = await collection.GetAsync("only-vector-data", cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(fetched);
            Assert.Equal(record.Id, fetched!.Id);
            Assert.Equal(record.Content, fetched.Content);
            Assert.Equal(vector, fetched.Embedding.ToArray());

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

    [Fact]
    public void VectorDataOnlyRecord_FilterTranslation_UsesGeneratedDefinitionMetadata()
    {
        System.Linq.Expressions.Expression<Func<VectorDataOnlyRecord, bool>> filter = record => record.Category == "books";
        string filterString = ZVecFilterExpressionVisitor.Translate(filter);
        Assert.Contains("Category", filterString, StringComparison.Ordinal);
        Assert.Contains("books", filterString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplicitDefinition_IsUsedWhenNoGeneratedSchemaRegistered()
    {
        string storagePath = CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);
        try
        {
            var definition = new VectorStoreCollectionDefinition
            {
                Properties =
                [
                    new VectorStoreKeyProperty(nameof(DefinitionOnlyRecord.Id), typeof(string)),
                    new VectorStoreDataProperty(nameof(DefinitionOnlyRecord.Content), typeof(string))
                    {
                        IsFullTextIndexed = true
                    },
                    new VectorStoreVectorProperty(nameof(DefinitionOnlyRecord.Embedding), typeof(ReadOnlyMemory<float>), 4)
                ]
            };

            var options = new ZVecVectorStoreOptions { StoragePath = storagePath };
            IZvecFactory factory = new ZVecFactory();
            factory.Initialize();

            var collection = new ZVecVectorizableRecordCollection<DefinitionOnlyRecord, string>(
                factory,
                options,
                "definition_override_" + Guid.NewGuid().ToString("N")[..8],
                definition);

            Assert.Same(definition, collection.Definition);
            await collection.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);
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
