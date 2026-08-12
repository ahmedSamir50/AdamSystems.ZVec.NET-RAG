using ZVec.NET;

namespace ZVec.Extensions.VectorData;

/// <summary>
/// Configuration options for registering ZVec.Extensions.VectorData services via Dependency Injection.
/// </summary>
public sealed class ZVecVectorStoreOptions
{
    private string _storagePath = string.Empty;

    /// <summary>
    /// Gets or sets the custom ZVec factory instance.
    /// If null, a default <see cref="ZVecFactory"/> singleton will be registered using <see cref="StoragePath"/>.
    /// </summary>
    public IZvecFactory? Factory { get; set; }

    /// <summary>
    /// Gets or sets the absolute or relative directory path where native ZVec collection
    /// files will be persisted. Defaults to <see cref="string.Empty"/> which selects the
    /// in-memory engine. Must be a valid absolute path when persistence is required.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when set to a relative path that cannot be resolved.</exception>
    public string StoragePath
    {
        get => _storagePath;
        set => _storagePath = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Path.GetFullPath(value);
    }

    /// <summary>
    /// Gets the effective collection base path. Returns <see cref="AppDomain.CurrentDomain.BaseDirectory"/>
    /// when <see cref="StoragePath"/> is empty (in-memory mode).
    /// </summary>
    internal string EffectiveCollectionBasePath => string.IsNullOrEmpty(_storagePath)
        ? AppDomain.CurrentDomain.BaseDirectory
        : _storagePath;
}
