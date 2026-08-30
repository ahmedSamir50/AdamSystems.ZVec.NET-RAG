using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using ZVec.Extensions.VectorData.Constants;
using ZVec.Extensions.VectorData.Store;
using ZVec.NET;

namespace ZVec.Extensions.VectorData.Manifest;

/// <summary>
/// Writes and validates the embedder stamp sidecar (<c>zvec_index_manifest.json</c>) for native collections.
/// </summary>
public static class ZVecIndexManifestManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        TypeInfoResolver = ZVecIndexManifestJsonContext.Default
    };

    /// <summary>
    /// Ensures the manifest exists and matches the configured embedder stamp when opening a collection.
    /// </summary>
    /// <param name="collectionPath">Absolute path to the native collection directory.</param>
    /// <param name="schema">Resolved native collection schema.</param>
    /// <param name="options">Active vector store options supplying <see cref="ZVecVectorStoreOptions.ModelId"/>.</param>
    /// <param name="collectionPreExistedOnDisk">Whether the collection directory contained files before open.</param>
    public static void EnsureManifest(
        string collectionPath,
        ZVecCollectionSchema schema,
        ZVecVectorStoreOptions options,
        bool collectionPreExistedOnDisk)
    {
        var expected = BuildExpectedManifest(schema, options);
        string manifestPath = Path.Combine(collectionPath, ZVecManifestFileNames.IndexManifest);

        if (!collectionPreExistedOnDisk)
        {
            WriteManifestAtomic(collectionPath, expected);
            return;
        }

        if (!File.Exists(manifestPath))
        {
            throw new ZVecManifestException(ZVecManifestFailureReason.Missing, collectionPath);
        }

        ZVecIndexManifest actual = ReadManifest(manifestPath, collectionPath);
        ValidateMatch(collectionPath, expected, actual);
    }

    /// <summary>
    /// Builds the expected stamp from schema metadata and store options.
    /// </summary>
    public static ZVecIndexManifest BuildExpectedManifest(
        ZVecCollectionSchema schema,
        ZVecVectorStoreOptions options)
    {
        var denseVector = ResolvePrimaryDenseVector(schema);
        var quantizeType = ResolveQuantizeType(denseVector, options);

        return new ZVecIndexManifest
        {
            ModelId = options.ModelId ?? string.Empty,
            Dimensions = denseVector?.Dimension ?? 0,
            QuantizeType = quantizeType.ToString(),
            StorageDataType = denseVector?.DataType.ToString() ?? ZVecDataType.VectorFp32.ToString(),
            CreatedUtc = DateTime.UtcNow
        };
    }

    private static ZVecVectorSchema? ResolvePrimaryDenseVector(ZVecCollectionSchema schema)
    {
        foreach (var vector in schema.Vectors)
        {
            if (vector.DataType is ZVecDataType.VectorFp32 or ZVecDataType.VectorFp16)
            {
                return vector;
            }
        }

        return schema.Vectors.FirstOrDefault();
    }

    private static ZVecQuantizeType ResolveQuantizeType(ZVecVectorSchema? denseVector, ZVecVectorStoreOptions options)
    {
        if (denseVector?.IndexParam is ZVecHnswIndexParam hnsw &&
            hnsw.QuantizeType != ZVecQuantizeType.Undefined)
        {
            return hnsw.QuantizeType;
        }

        return options.DefaultQuantizeType;
    }

    private static ZVecIndexManifest ReadManifest(string manifestPath, string collectionPath)
    {
        try
        {
            string json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize(json, ZVecIndexManifestJsonContext.Default.ZVecIndexManifest);
            if (manifest == null)
            {
                throw new ZVecManifestException(ZVecManifestFailureReason.Corrupt, collectionPath);
            }

            return manifest;
        }
        catch (ZVecManifestException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw new ZVecManifestException(ZVecManifestFailureReason.Corrupt, collectionPath);
        }
    }

    private static void ValidateMatch(string collectionPath, ZVecIndexManifest expected, ZVecIndexManifest actual)
    {
        if (expected.ModelId == actual.ModelId &&
            expected.Dimensions == actual.Dimensions &&
            string.Equals(expected.QuantizeType, actual.QuantizeType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(expected.StorageDataType, actual.StorageDataType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new ZVecEmbedderMismatchException(
            collectionPath,
            expected.ModelId,
            actual.ModelId,
            expected.Dimensions,
            actual.Dimensions,
            expected.QuantizeType,
            actual.QuantizeType,
            expected.StorageDataType,
            actual.StorageDataType);
    }

    private static void WriteManifestAtomic(string collectionPath, ZVecIndexManifest manifest)
    {
        Directory.CreateDirectory(collectionPath);
        string finalPath = Path.Combine(collectionPath, ZVecManifestFileNames.IndexManifest);
        string tempPath = Path.Combine(collectionPath, ZVecManifestFileNames.IndexManifestTemp);

        string json = JsonSerializer.Serialize(manifest, ZVecIndexManifestJsonContext.Default.ZVecIndexManifest);
        File.WriteAllText(tempPath, json);

        if (File.Exists(finalPath))
        {
            File.Replace(tempPath, finalPath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, finalPath);
        }
    }
}
