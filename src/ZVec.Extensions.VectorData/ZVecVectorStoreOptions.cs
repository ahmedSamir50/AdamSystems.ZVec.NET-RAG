using ZVec.NET;

namespace ZVec.Extensions.VectorData;

/// <summary>
/// Configuration options for registering ZVec.Extensions.VectorData services via Dependency Injection.
/// </summary>
public sealed class ZVecVectorStoreOptions
{
    /// <summary>
    /// Gets or sets the custom ZVec factory instance.
    /// If null, a default <see cref="ZVecFactory"/> singleton will be registered.
    /// </summary>
    public IZvecFactory? Factory { get; set; }

    /// <summary>
    /// Gets or sets the default ZVec storage directory path.
    /// Defaults to empty string (in-memory storage mode).
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;
}
