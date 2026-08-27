namespace ZVec.Extensions.VectorData.Manifest;

/// <summary>
/// Reason codes for <see cref="ZVecManifestException"/> when the sidecar manifest is invalid.
/// </summary>
public enum ZVecManifestFailureReason
{
    /// <summary>Native collection exists but the manifest file is absent.</summary>
    Missing,

    /// <summary>Manifest file exists but cannot be parsed.</summary>
    Corrupt
}
