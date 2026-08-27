namespace ZVec.Extensions.VectorData.Constants;

/// <summary>
/// File names for ZVec collection sidecar metadata on disk.
/// </summary>
public static class ZVecManifestFileNames
{
    /// <summary>Final embedder stamp manifest file name.</summary>
    public const string IndexManifest = "zvec_index_manifest.json";

    /// <summary>Temporary manifest file written before atomic replace.</summary>
    public const string IndexManifestTemp = "zvec_index_manifest.json.tmp";
}
