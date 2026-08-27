using ZVec.NET;

namespace ZVec.Extensions.VectorData.Store;

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

    /// <summary>
    /// Gets or sets the maximum number of concurrent native calls allowed by the ZVec engine.
    /// Defaults to <see cref="Environment.ProcessorCount"/>.
    /// </summary>
    public int MaxConcurrentNativeCalls { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets whether native collection files are opened with memory-mapped I/O.
    /// Defaults to <c>true</c> (matches <see cref="ZVecCollectionOptions.EnableMmap"/> engine default).
    /// Recommended for mobile read-only indexes shipped from desktop ingest.
    /// </summary>
    public bool EnableMmap { get; set; } = true;

    /// <summary>
    /// Gets or sets whether collections are opened read-only (no upsert/delete at the native layer).
    /// Defaults to <c>false</c>. Pair with <see cref="EnableMmap"/> for shipped mobile indexes.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Gets or sets the process-wide native memory limit in megabytes.
    /// When null, the engine default is used. Maps to <see cref="ZVecOptions.MemoryLimitMb"/>.
    /// </summary>
    public int? MemoryLimitMb { get; set; }

    /// <summary>
    /// Gets or sets the embedder model identifier recorded in the index manifest sidecar.
    /// Used to detect silent corruption when switching embedding models without re-ingest.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default HNSW/Flat vector index quantization applied to dense vectors
    /// when no per-property override exists. Defaults to <see cref="ZVecQuantizeType.Undefined"/> (FP32 storage).
    /// </summary>
    public ZVecQuantizeType DefaultQuantizeType { get; set; } = ZVecQuantizeType.Undefined;

    /// <summary>
    /// Creates a <see cref="ZVecOptions"/> snapshot from the current vector store configuration.
    /// </summary>
    internal ZVecOptions CreateZVecOptions() => new()
    {
        MaxConcurrentNativeCalls = MaxConcurrentNativeCalls,
        MemoryLimitMb = MemoryLimitMb
    };

    /// <summary>
    /// Creates a <see cref="ZVecCollectionOptions"/> snapshot from the current vector store configuration.
    /// </summary>
    internal ZVecCollectionOptions CreateZVecCollectionOptions() => new()
    {
        EnableMmap = EnableMmap,
        ReadOnly = ReadOnly
    };
}
