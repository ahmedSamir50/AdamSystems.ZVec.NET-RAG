using System.Runtime.CompilerServices;
using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData.Constants;
using ZVec.NET;

namespace ZVec.Extensions.VectorData;

/// <summary>
/// Implements Microsoft's <see cref="VectorStore"/> abstract base class over embedded vector database engine <see cref="IZvecFactory"/>.
/// </summary>
/// <remarks>
/// <para>
/// Collection Architecture &amp; Mapping:
/// Each record type maps to a named native ZVec collection.
/// <code>
/// ┌─────────────────────────────────────────────────────────────┐
/// │                     ZVecVectorStore                         │
/// ├─────────────────────────────────────────────────────────────┤
/// │  GetCollection&lt;TKey, TRecord&gt;("documents")                 │
/// │   │                                                         │
/// │   ▼                                                         │
/// │  ZVecVectorizableRecordCollection&lt;TRecord, TKey&gt;           │
/// │   │                                                         │
/// │   ▼                                                         │
/// │  Native ZVec Collection ("documents")                       │
/// └─────────────────────────────────────────────────────────────┘
/// </code>
/// </para>
/// </remarks>
public sealed class ZVecVectorStore : VectorStore
{
    private readonly IZvecFactory _factory;
    private readonly ZVecVectorStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="ZVecVectorStore"/> backed by <see cref="IZvecFactory"/>.
    /// </summary>
    /// <param name="factory">Process-wide ZVec native factory instance.</param>
    /// <param name="options">Vector store options providing StoragePath for collection enumeration.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> or <paramref name="options"/> is null.</exception>
    public ZVecVectorStore(IZvecFactory factory, ZVecVectorStoreOptions options)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(
        string name,
        VectorStoreCollectionDefinition? definition = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(ZVecErrorMessages.NullOrEmptyCollectionName, nameof(name));
        }

        return new ZVecVectorizableRecordCollection<TRecord, TKey>(_factory, _options, name, definition);
    }

    /// <inheritdoc />
    public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(
        string name,
        VectorStoreCollectionDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(ZVecErrorMessages.NullOrEmptyCollectionName, nameof(name));
        }

        return new ZVecVectorizableRecordCollection<Dictionary<string, object?>, object>(_factory, _options, name, definition);
    }

    /// <inheritdoc />
    public override Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(ZVecErrorMessages.NullOrEmptyCollectionName, nameof(name));

        cancellationToken.ThrowIfCancellationRequested();

        string collectionPath = Path.Combine(_options.EffectiveCollectionBasePath, name);
        bool exists = Directory.Exists(collectionPath) && Directory.EnumerateFileSystemEntries(collectionPath).Any();

        return Task.FromResult(exists);
    }

    /// <inheritdoc />
    public override Task EnsureCollectionDeletedAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(ZVecErrorMessages.NullOrEmptyCollectionName, nameof(name));

        cancellationToken.ThrowIfCancellationRequested();

        string collectionPath = Path.Combine(_options.EffectiveCollectionBasePath, name);
        if (Directory.Exists(collectionPath))
        {
            try
            {
                Directory.Delete(collectionPath, recursive: true);
            }
            catch
            {
                // Best effort directory cleanup
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(IZvecFactory))
        {
            return _factory;
        }

        return null;
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<string> ListCollectionNamesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();

        string basePath = _options.EffectiveCollectionBasePath;
        if (!Directory.Exists(basePath))
        {
            yield break;
        }

        // Filter out non-collection directories. Native ZVec collections are detected by
        // the presence of a marker file (zvec_collection.json or similar) — if no marker
        // file convention exists, fall back to excluding known infrastructure directories.
        var excludedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", "logs", "node_modules", ".vs", ".idea", ".git"
        };

        foreach (var dir in Directory.EnumerateDirectories(basePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dirName = Path.GetFileName(dir);

            if (string.IsNullOrEmpty(dirName)) continue;
            if (dirName.StartsWith(".")) continue;
            if (excludedNames.Contains(dirName)) continue;

            yield return dirName;
        }
    }
}
