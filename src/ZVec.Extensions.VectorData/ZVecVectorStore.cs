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

    /// <summary>
    /// Initializes a new instance of <see cref="ZVecVectorStore"/> backed by <see cref="IZvecFactory"/>.
    /// </summary>
    /// <param name="factory">Process-wide ZVec native factory instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
    public ZVecVectorStore(IZvecFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
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

        return new ZVecVectorizableRecordCollection<TRecord, TKey>(_factory, name, definition);
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

        return new ZVecVectorizableRecordCollection<Dictionary<string, object?>, object>(_factory, name, definition);
    }

    /// <inheritdoc />
    public override Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(ZVecErrorMessages.NullOrEmptyCollectionName, nameof(name));

        cancellationToken.ThrowIfCancellationRequested();

        string basePath = AppDomain.CurrentDomain.BaseDirectory;
        string collectionPath = Path.Combine(basePath, name);
        bool exists = Directory.Exists(collectionPath) && Directory.EnumerateFileSystemEntries(collectionPath).Any();

        return Task.FromResult(exists);
    }

    /// <inheritdoc />
    public override Task EnsureCollectionDeletedAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(ZVecErrorMessages.NullOrEmptyCollectionName, nameof(name));

        cancellationToken.ThrowIfCancellationRequested();

        string basePath = AppDomain.CurrentDomain.BaseDirectory;
        string collectionPath = Path.Combine(basePath, name);
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

        string basePath = AppDomain.CurrentDomain.BaseDirectory;
        if (Directory.Exists(basePath))
        {
            foreach (var dir in Directory.EnumerateDirectories(basePath))
            {
                var dirName = Path.GetFileName(dir);
                if (!string.IsNullOrEmpty(dirName) && !dirName.StartsWith("."))
                {
                    yield return dirName;
                }
            }
        }
    }
}
