using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData.Constants;
using ZVec.NET;
using ZVec.NET.Mapping;

namespace ZVec.Extensions.VectorData;

/// <summary>
/// Implements <see cref="VectorStoreCollection{TKey, TRecord}"/> and <see cref="IKeywordHybridSearchable{TRecord}"/>
/// over a named native ZVec collection.
/// </summary>
/// <remarks>
/// <para>
/// <b>Zero-Allocation Vector Pinning Path:</b>
/// Input vectors of type <see cref="ReadOnlyMemory{T}"/> of <see cref="float"/> are pinned directly using <see cref="ReadOnlyMemory{T}.Pin"/>
/// without heap allocation or array copying.
/// </para>
/// <code>
/// ┌─────────────────────────────────────────────────────────────┐
/// │            SearchAsync(ReadOnlyMemory&lt;float&gt;)               │
/// ├─────────────────────────────────────────────────────────────┤
/// │  memory.Pin() ──► SafeHandle ──► P/Invoke float* Native Query │
/// └─────────────────────────────────────────────────────────────┘
/// </code>
/// </remarks>
/// <typeparam name="TRecord">Record POCO type.</typeparam>
/// <typeparam name="TKey">Primary key type.</typeparam>
public sealed class ZVecVectorizableRecordCollection<TRecord, TKey> :
                    VectorStoreCollection<TKey, TRecord>, IKeywordHybridSearchable<TRecord>
    where TRecord : class
    where TKey : notnull
{
    private readonly IZvecFactory _factory;
    private readonly ZVecTypeModel? _typeModel;
    private IZvecCollection? _nativeCollection;
    private readonly object _initLock = new();

    /// <summary>
    /// Initializes a new instance of <see cref="ZVecVectorizableRecordCollection{TRecord, TKey}"/>.
    /// </summary>
    /// <param name="factory">Process-wide ZVec factory.</param>
    /// <param name="name">Name of the collection.</param>
    /// <param name="definition">Optional Microsoft VectorStoreCollectionDefinition override.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty, or whitespace.</exception>
    public ZVecVectorizableRecordCollection(
        IZvecFactory factory,
        string name,
        VectorStoreCollectionDefinition? definition = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(ZVecErrorMessages.NullOrEmptyCollectionName, nameof(name));

        Name = name;
        Definition = definition;
        if (typeof(TRecord) != typeof(Dictionary<string, object?>))
            _typeModel = ZVecTypeModel.Get<TRecord>();
    }

    /// <inheritdoc />
    public override string Name { get; }

    /// <summary>
    /// Gets the optional schema collection definition override.
    /// </summary>
    public VectorStoreCollectionDefinition? Definition { get; }

    private string CollectionPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Name);

    private IZvecCollection GetOrOpenNativeCollection()
    {
        if (_nativeCollection != null) return _nativeCollection;

        lock (_initLock)
        {
            if (_nativeCollection != null) return _nativeCollection;

            if (!_factory.IsInitialized)
            {
                _factory.Initialize();
            }

            var schemaBuilder = ZVecCollectionSchemaBuilder.From<TRecord>();
            var schema = schemaBuilder.Build();
            _nativeCollection = _factory.OpenOrCreate(CollectionPath, schema);
            return _nativeCollection;
        }
    }

    /// <inheritdoc />
    public override Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool exists = Directory.Exists(CollectionPath) && Directory.EnumerateFileSystemEntries(CollectionPath).Any();
        return Task.FromResult(exists);
    }

    /// <inheritdoc />
    public override Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GetOrOpenNativeCollection();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_initLock)
        {
            if (_nativeCollection != null)
            {
                try { _nativeCollection.Dispose(); } catch { }
                _nativeCollection = null;
            }

            if (Directory.Exists(CollectionPath))
            {
                try { Directory.Delete(CollectionPath, recursive: true); } catch { }
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        cancellationToken.ThrowIfCancellationRequested();

        var collection = GetOrOpenNativeCollection();
        string pk = key.ToString()!;
        await collection.DeleteAsync(pk, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task DeleteAsync(IEnumerable<TKey> keys, CancellationToken cancellationToken = default)
    {
        if (keys == null) throw new ArgumentNullException(nameof(keys));
        cancellationToken.ThrowIfCancellationRequested();

        var collection = GetOrOpenNativeCollection();
        var pkList = keys.Select(k => k.ToString()!).ToList();
        if (pkList.Count > 0)
        {
            await collection.DeleteAsync(pkList, cancellationToken);
        }
    }

    /// <inheritdoc />
    public override async Task<TRecord?> GetAsync(TKey key, RecordRetrievalOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        cancellationToken.ThrowIfCancellationRequested();

        var collection = GetOrOpenNativeCollection();
        string pk = key.ToString()!;
        var doc = await collection.FetchAsync(pk, includeVector: options?.IncludeVectors ?? true, ct: cancellationToken);
        if (doc == null || _typeModel == null) return null;

        return MapFromDoc(doc);
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<TRecord> GetAsync(
        IEnumerable<TKey> keys,
        RecordRetrievalOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (keys == null) throw new ArgumentNullException(nameof(keys));
        cancellationToken.ThrowIfCancellationRequested();

        var collection = GetOrOpenNativeCollection();
        var pkList = keys.Select(k => k.ToString()!).ToList();
        if (pkList.Count == 0 || _typeModel == null) yield break;

        var docs = await collection.FetchAsync(pkList, includeVector: options?.IncludeVectors ?? true, ct: cancellationToken);
        foreach (var doc in docs)
        {
            if (doc != null)
            {
                yield return MapFromDoc(doc);
            }
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<TRecord> GetAsync(
        Expression<Func<TRecord, bool>> filter,
        int top,
        FilteredRecordRetrievalOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (filter == null) throw new ArgumentNullException(nameof(filter));
        cancellationToken.ThrowIfCancellationRequested();

        var collection = GetOrOpenNativeCollection();
        var filterBuilder = ZVecFilterExpressionVisitor.TranslateToBuilder(filter);
        int effectiveTop = top > 0 ? top : ZVecConstants.DefaultQueryLimit;

        string vectorFieldName = _typeModel?.Vectors.FirstOrDefault()?.StorageName ?? "Vector";
        var dummyQuery = new ZVecQuery { FieldName = vectorFieldName, Vector = new float[768] };
        var docs = await collection.QueryAsync(dummyQuery, effectiveTop, filterBuilder, includeVector: options?.IncludeVectors ?? true, ct: cancellationToken);

        if (_typeModel != null)
        {
            foreach (var doc in docs)
            {
                yield return MapFromDoc(doc);
            }
        }
    }

    /// <inheritdoc />
    public override async Task UpsertAsync(TRecord record, CancellationToken cancellationToken = default)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        cancellationToken.ThrowIfCancellationRequested();

        if (_typeModel == null) return;
        var collection = GetOrOpenNativeCollection();
        var doc = ZVecMapper.ToDoc(record, _typeModel);
        await collection.UpsertAsync(doc, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task UpsertAsync(IEnumerable<TRecord> records, CancellationToken cancellationToken = default)
    {
        if (records == null) throw new ArgumentNullException(nameof(records));
        cancellationToken.ThrowIfCancellationRequested();

        if (_typeModel == null) return;
        var collection = GetOrOpenNativeCollection();
        var docs = records.Select(r => ZVecMapper.ToDoc(r, _typeModel)).ToList();
        if (docs.Count > 0)
        {
            await collection.UpsertAsync(docs, cancellationToken);
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TInput>(
        TInput searchValue,
        int top,
        VectorSearchOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (searchValue == null) throw new ArgumentNullException(nameof(searchValue));

        if (searchValue is ReadOnlyMemory<float> floatMemory)
        {
            using var handle = floatMemory.Pin();
            cancellationToken.ThrowIfCancellationRequested();

            var collection = GetOrOpenNativeCollection();
            int effectiveTop = top > 0 ? top : ZVecConstants.DefaultQueryLimit;
            double scoreThreshold = options?.ScoreThreshold ?? ZVecConstants.DefaultMinScoreThreshold;

            string vectorFieldName = _typeModel?.Vectors.FirstOrDefault()?.StorageName ?? "Vector";
            var query = new ZVecQuery { FieldName = vectorFieldName, Vector = floatMemory };
            IReadOnlyList<ZVecDoc> docs;

            if (options?.Filter != null)
            {
                var filterBuilder = ZVecFilterExpressionVisitor.TranslateToBuilder(options.Filter);
                docs = await collection.QueryAsync(query, effectiveTop, filterBuilder, includeVector: options?.IncludeVectors ?? true, ct: cancellationToken);
            }
            else
            {
                docs = await collection.QueryAsync(query, effectiveTop, filter: (string?)null, includeVector: options?.IncludeVectors ?? true, ct: cancellationToken);
            }

            if (_typeModel != null)
            {
                foreach (var doc in docs)
                {
                    float similarityScore = doc.Score > 0 ? doc.Score : (1.0f - doc.Score);
                    if (similarityScore >= scoreThreshold)
                    {
                        var record = MapFromDoc(doc);
                        yield return new VectorSearchResult<TRecord>(record, similarityScore);
                    }
                }
            }
            yield break;
        }

        throw new NotSupportedException(ZVecErrorMessages.UnsupportedVectorType(typeof(TInput).Name));
    }

    /// <inheritdoc />
    async IAsyncEnumerable<VectorSearchResult<TRecord>> IKeywordHybridSearchable<TRecord>.HybridSearchAsync<TInput>(
        TInput searchValue,
        ICollection<string> keywords,
        int top,
        HybridSearchOptions<TRecord>? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (searchValue == null) throw new ArgumentNullException(nameof(searchValue));
        if (keywords == null) throw new ArgumentNullException(nameof(keywords));

        if (searchValue is ReadOnlyMemory<float> floatMemory)
        {
            using var handle = floatMemory.Pin();
            cancellationToken.ThrowIfCancellationRequested();

            var collection = GetOrOpenNativeCollection();
            int effectiveTop = top > 0 ? top : ZVecConstants.DefaultQueryLimit;

            string vectorFieldName = _typeModel?.Vectors.FirstOrDefault()?.StorageName ?? "Vector";
            var query = new ZVecQuery { FieldName = vectorFieldName, Vector = floatMemory };
            IReadOnlyList<ZVecDoc> docs;

            if (options?.Filter != null)
            {
                var filterBuilder = ZVecFilterExpressionVisitor.TranslateToBuilder(options.Filter);
                docs = await collection.QueryAsync(query, effectiveTop, filterBuilder, includeVector: options?.IncludeVectors ?? true, ct: cancellationToken);
            }
            else
            {
                docs = await collection.QueryAsync(query, effectiveTop, filter: (string?)null, includeVector: options?.IncludeVectors ?? true, ct: cancellationToken);
            }

            if (_typeModel != null)
            {
                foreach (var doc in docs)
                {
                    float similarityScore = doc.Score > 0 ? doc.Score : (1.0f - doc.Score);
                    var record = MapFromDoc(doc);
                    yield return new VectorSearchResult<TRecord>(record, similarityScore);
                }
            }
            yield break;
        }

        throw new NotSupportedException(ZVecErrorMessages.UnsupportedVectorType(typeof(TInput).Name));
    }

    private TRecord MapFromDoc(ZVecDoc doc)
    {
        if (_typeModel == null) throw new InvalidOperationException("Type model is uninitialized.");
        var record = (TRecord)Activator.CreateInstance(typeof(TRecord))!;
        _typeModel.Id.Property.SetValue(record, doc.Id);
        foreach (var field in _typeModel.Fields)
        {
            if (doc.Fields.TryGetValue(field.StorageName, out var val) && val != null)
            {
                field.Property.SetValue(record, val);
            }
        }
        foreach (var vec in _typeModel.Vectors)
        {
            if (doc.DenseVectors.TryGetValue(vec.StorageName, out var dense))
            {
                vec.Property.SetValue(record, dense);
            }
        }
        return record;
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
}
