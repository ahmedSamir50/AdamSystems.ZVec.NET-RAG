using System.Text.Json.Serialization;
using ZVec.NET;

namespace ZVec.Extensions.VectorData.Manifest;

/// <summary>
/// Embedder stamp sidecar persisted as <c>zvec_index_manifest.json</c> beside a native collection.
/// </summary>
public sealed class ZVecIndexManifest
{
    /// <summary>Embedding model identifier (e.g. nomic-embed-text).</summary>
    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>Vector dimension count for the primary dense embedding field.</summary>
    [JsonPropertyName("dimensions")]
    public int Dimensions { get; set; }

    /// <summary>HNSW/Flat quantization mode name.</summary>
    [JsonPropertyName("quantizeType")]
    public string QuantizeType { get; set; } = ZVecQuantizeType.Undefined.ToString();

    /// <summary>Native vector storage data type name (e.g. VectorFp32).</summary>
    [JsonPropertyName("storageDataType")]
    public string StorageDataType { get; set; } = ZVecDataType.VectorFp32.ToString();

    /// <summary>UTC timestamp when the manifest was first written.</summary>
    [JsonPropertyName("createdUtc")]
    public DateTime CreatedUtc { get; set; }
}
