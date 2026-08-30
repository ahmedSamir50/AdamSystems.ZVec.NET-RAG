using System.Text.Json.Serialization;

namespace ZVec.Extensions.VectorData.Manifest;

/// <summary>
/// AOT-safe JSON serialization context for embedder stamp manifests.
/// </summary>
[JsonSerializable(typeof(ZVecIndexManifest))]
internal partial class ZVecIndexManifestJsonContext : JsonSerializerContext;
