using ZVec.Extensions.VectorData.Constants;
using ZVec.Extensions.VectorData.Exceptions;

namespace ZVec.Extensions.VectorData.Manifest;

/// <summary>
/// Thrown when the embedder stamp manifest is missing or corrupt on an existing collection.
/// </summary>
public sealed class ZVecManifestException : ZVecVectorDataException
{
    /// <summary>
    /// Initializes a new instance with reason and remediation guidance.
    /// </summary>
    /// <param name="reason">Whether the manifest is missing or corrupt.</param>
    /// <param name="collectionPath">Absolute path to the native collection directory.</param>
    public ZVecManifestException(ZVecManifestFailureReason reason, string collectionPath)
        : base(BuildMessage(reason, collectionPath))
    {
        Reason = reason;
        CollectionPath = collectionPath;
    }

    /// <summary>Failure category distinguishing missing vs corrupt manifests.</summary>
    public ZVecManifestFailureReason Reason { get; }

    /// <summary>Path to the native collection directory.</summary>
    public string CollectionPath { get; }

    private static string BuildMessage(ZVecManifestFailureReason reason, string collectionPath)
    {
        return reason switch
        {
            ZVecManifestFailureReason.Missing =>
                ZVecErrorMessages.ManifestMissing(collectionPath),
            ZVecManifestFailureReason.Corrupt =>
                ZVecErrorMessages.ManifestCorrupt(collectionPath),
            _ => ZVecErrorMessages.ManifestCorrupt(collectionPath)
        };
    }
}
