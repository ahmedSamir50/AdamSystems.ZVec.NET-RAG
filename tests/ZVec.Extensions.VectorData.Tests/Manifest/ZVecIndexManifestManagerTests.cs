using System.Text.Json;
using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData.Constants;
using ZVec.Extensions.VectorData.Manifest;
using ZVec.NET;
using ZVec.NET.Mapping;
using Xunit;

namespace ZVec.Extensions.VectorData.Tests;

/// <summary>
/// TDD tests for embedder stamp manifest creation and validation (Story 1.11).
/// </summary>
public sealed class ZVecIndexManifestManagerTests
{
    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "ZVecTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void EnsureManifest_WritesSidecar_OnFirstCollectionCreation()
    {
        string collectionPath = Path.Combine(CreateTempStoragePath(), "manifest_col");
        var options = new ZVecVectorStoreOptions { ModelId = "nomic-embed-text" };
        var schema = BuildTestSchema(ZVecQuantizeType.Undefined, ZVecDataType.VectorFp32, 768);

        ZVecIndexManifestManager.EnsureManifest(collectionPath, schema, options, collectionPreExistedOnDisk: false);

        string manifestPath = Path.Combine(collectionPath, ZVecManifestFileNames.IndexManifest);
        Assert.True(File.Exists(manifestPath));

        var manifest = JsonSerializer.Deserialize<ZVecIndexManifest>(File.ReadAllText(manifestPath));
        Assert.NotNull(manifest);
        Assert.Equal("nomic-embed-text", manifest!.ModelId);
        Assert.Equal(768, manifest.Dimensions);
        Assert.Equal(ZVecQuantizeType.Undefined.ToString(), manifest.QuantizeType);
        Assert.Equal(ZVecDataType.VectorFp32.ToString(), manifest.StorageDataType);
        Assert.NotEqual(default, manifest.CreatedUtc);

        try { Directory.Delete(Path.GetDirectoryName(collectionPath)!, recursive: true); } catch { }
    }

    [Fact]
    public void EnsureManifest_Succeeds_WhenStampMatchesOnReopen()
    {
        string collectionPath = Path.Combine(CreateTempStoragePath(), "reopen_col");
        var options = new ZVecVectorStoreOptions { ModelId = "miniLM" };
        var schema = BuildTestSchema(ZVecQuantizeType.Undefined, ZVecDataType.VectorFp32, 384);

        ZVecIndexManifestManager.EnsureManifest(collectionPath, schema, options, collectionPreExistedOnDisk: false);
        ZVecIndexManifestManager.EnsureManifest(collectionPath, schema, options, collectionPreExistedOnDisk: true);

        try { Directory.Delete(Path.GetDirectoryName(collectionPath)!, recursive: true); } catch { }
    }

    [Fact]
    public void EnsureManifest_ThrowsEmbedderMismatch_WhenModelIdDiffers()
    {
        string collectionPath = Path.Combine(CreateTempStoragePath(), "mismatch_model");
        var schema = BuildTestSchema(ZVecQuantizeType.Undefined, ZVecDataType.VectorFp32, 768);
        var createOptions = new ZVecVectorStoreOptions { ModelId = "model-a" };
        var reopenOptions = new ZVecVectorStoreOptions { ModelId = "model-b" };

        ZVecIndexManifestManager.EnsureManifest(collectionPath, schema, createOptions, collectionPreExistedOnDisk: false);

        var ex = Assert.Throws<ZVecEmbedderMismatchException>(() =>
            ZVecIndexManifestManager.EnsureManifest(collectionPath, schema, reopenOptions, collectionPreExistedOnDisk: true));

        Assert.Equal("model-b", ex.ExpectedModelId);
        Assert.Equal("model-a", ex.ActualModelId);
        Assert.Contains(collectionPath, ex.Message);

        try { Directory.Delete(Path.GetDirectoryName(collectionPath)!, recursive: true); } catch { }
    }

    [Fact]
    public void EnsureManifest_ThrowsEmbedderMismatch_WhenDimensionsDiffer()
    {
        string collectionPath = Path.Combine(CreateTempStoragePath(), "mismatch_dim");
        var createSchema = BuildTestSchema(ZVecQuantizeType.Undefined, ZVecDataType.VectorFp32, 768);
        var reopenSchema = BuildTestSchema(ZVecQuantizeType.Undefined, ZVecDataType.VectorFp32, 384);
        var options = new ZVecVectorStoreOptions { ModelId = "same-model" };

        ZVecIndexManifestManager.EnsureManifest(collectionPath, createSchema, options, collectionPreExistedOnDisk: false);

        var ex = Assert.Throws<ZVecEmbedderMismatchException>(() =>
            ZVecIndexManifestManager.EnsureManifest(collectionPath, reopenSchema, options, collectionPreExistedOnDisk: true));

        Assert.Equal(384, ex.ExpectedDimensions);
        Assert.Equal(768, ex.ActualDimensions);

        try { Directory.Delete(Path.GetDirectoryName(collectionPath)!, recursive: true); } catch { }
    }

    [Fact]
    public void EnsureManifest_ThrowsEmbedderMismatch_WhenQuantizeTypeDiffers()
    {
        string collectionPath = Path.Combine(CreateTempStoragePath(), "mismatch_quant");
        var createSchema = BuildTestSchema(ZVecQuantizeType.Undefined, ZVecDataType.VectorFp32, 128);
        var reopenSchema = BuildTestSchema(ZVecQuantizeType.Int8, ZVecDataType.VectorFp32, 128);
        var options = new ZVecVectorStoreOptions { ModelId = "q-model" };

        ZVecIndexManifestManager.EnsureManifest(collectionPath, createSchema, options, collectionPreExistedOnDisk: false);

        Assert.Throws<ZVecEmbedderMismatchException>(() =>
            ZVecIndexManifestManager.EnsureManifest(collectionPath, reopenSchema, options, collectionPreExistedOnDisk: true));

        try { Directory.Delete(Path.GetDirectoryName(collectionPath)!, recursive: true); } catch { }
    }

    [Fact]
    public void EnsureManifest_ThrowsManifestException_WhenManifestMissing()
    {
        string collectionPath = Path.Combine(CreateTempStoragePath(), "missing_manifest");
        Directory.CreateDirectory(collectionPath);
        File.WriteAllText(Path.Combine(collectionPath, "native_marker.dat"), "x");

        var schema = BuildTestSchema(ZVecQuantizeType.Undefined, ZVecDataType.VectorFp32, 128);
        var options = new ZVecVectorStoreOptions { ModelId = "test" };

        var ex = Assert.Throws<ZVecManifestException>(() =>
            ZVecIndexManifestManager.EnsureManifest(collectionPath, schema, options, collectionPreExistedOnDisk: true));

        Assert.Equal(ZVecManifestFailureReason.Missing, ex.Reason);

        try { Directory.Delete(Path.GetDirectoryName(collectionPath)!, recursive: true); } catch { }
    }

    [Fact]
    public void EnsureManifest_ThrowsManifestException_WhenManifestCorrupt()
    {
        string collectionPath = Path.Combine(CreateTempStoragePath(), "corrupt_manifest");
        Directory.CreateDirectory(collectionPath);
        File.WriteAllText(Path.Combine(collectionPath, ZVecManifestFileNames.IndexManifest), "{ not-json");

        var schema = BuildTestSchema(ZVecQuantizeType.Undefined, ZVecDataType.VectorFp32, 128);
        var options = new ZVecVectorStoreOptions { ModelId = "test" };

        var ex = Assert.Throws<ZVecManifestException>(() =>
            ZVecIndexManifestManager.EnsureManifest(collectionPath, schema, options, collectionPreExistedOnDisk: true));

        Assert.Equal(ZVecManifestFailureReason.Corrupt, ex.Reason);

        try { Directory.Delete(Path.GetDirectoryName(collectionPath)!, recursive: true); } catch { }
    }

    [Fact]
    public async Task EnsureCollectionDeletedAsync_RemovesManifestWithCollectionDirectory()
    {
        string storagePath = CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);
        var options = new ZVecVectorStoreOptions { StoragePath = storagePath, ModelId = "delete-test" };
        var factory = new ZVecFactory();
        factory.Initialize();

        try
        {
            var collection = new ZVecVectorizableRecordCollection<ManifestTestRecord, string>(
                factory, options, "delete_manifest_col");
            await collection.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);

            string manifestPath = Path.Combine(storagePath, "delete_manifest_col", ZVecManifestFileNames.IndexManifest);
            Assert.True(File.Exists(manifestPath));

            await collection.EnsureCollectionDeletedAsync(TestContext.Current.CancellationToken);
            Assert.False(Directory.Exists(Path.Combine(storagePath, "delete_manifest_col")));
        }
        finally
        {
            factory.Shutdown();
            if (Directory.Exists(storagePath))
            {
                try { Directory.Delete(storagePath, recursive: true); } catch { }
            }
        }
    }

    private static ZVecCollectionSchema BuildTestSchema(
        ZVecQuantizeType quantizeType,
        ZVecDataType storageType,
        int dimensions)
    {
        return new ZVecCollectionSchema
        {
            Name = "test_schema",
            Fields = Array.Empty<ZVecFieldSchema>(),
            Vectors = new[]
            {
                new ZVecVectorSchema
                {
                    Name = "Vector",
                    DataType = storageType,
                    Dimension = dimensions,
                    IndexParam = new ZVecHnswIndexParam { QuantizeType = quantizeType }
                }
            }
        };
    }

    private sealed class ManifestTestRecord
    {
        [ZVecId]
        [VectorStoreKey]
        public string Id { get; set; } = string.Empty;

        [ZVecVector(128)]
        [VectorStoreVector(128)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }
}
