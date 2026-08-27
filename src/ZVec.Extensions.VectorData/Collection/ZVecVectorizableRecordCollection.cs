using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData.Attributes;
using ZVec.Extensions.VectorData.Constants;
using ZVec.Extensions.VectorData.Filter;
using ZVec.Extensions.VectorData.Hybrid;
using ZVec.Extensions.VectorData.Mapping;
using ZVec.Extensions.VectorData.Store;
using ZVec.NET;
using ZVec.NET.Mapping;
using ZVec.NET.Query;

namespace ZVec.Extensions.VectorData.Collection;

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
public sealed partial class ZVecVectorizableRecordCollection<TRecord, TKey> :
                    VectorStoreCollection<TKey, TRecord>, IKeywordHybridSearchable<TRecord>
    where TRecord : class
    where TKey : notnull
{
    private readonly IZvecFactory _factory;
    private readonly ZVecVectorStoreOptions _options;
    private readonly ZVecTypeModel? _typeModel;
    private readonly IZVecRecordMapper<TRecord>? _mapper;
    private IZvecCollection? _nativeCollection;
    private readonly object _initLock = new();

    /// <summary>
    /// Initializes a new instance of <see cref="ZVecVectorizableRecordCollection{TRecord, TKey}"/>.
    /// </summary>
    /// <param name="factory">Process-wide ZVec factory.</param>
    /// <param name="options">Vector store options providing <see cref="ZVecVectorStoreOptions.StoragePath"/>.</param>
    /// <param name="name">Name of the collection.</param>
    /// <param name="definition">Optional Microsoft VectorStoreCollectionDefinition override.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty, or whitespace.</exception>
    public ZVecVectorizableRecordCollection(
        IZvecFactory factory,
        ZVecVectorStoreOptions options,
        string name,
        VectorStoreCollectionDefinition? definition = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(ZVecErrorMessages.NullOrEmptyCollectionName, nameof(name));

        Name = name;
        _mapper = ZVecRecordMapperRegistry.Get<TRecord>();
        Definition = definition ?? ZVecCollectionSchemaRegistry.GetDefinition<TRecord>();
        if (typeof(TRecord) != typeof(Dictionary<string, object?>))
        {
            if (_mapper == null && Definition == null)
            {
                _typeModel = ZVecTypeModel.Get<TRecord>();
            }
        }
    }

    /// <inheritdoc />
    public override string Name { get; }

    /// <summary>
    /// Gets the optional schema collection definition override.
    /// </summary>
    public VectorStoreCollectionDefinition? Definition { get; }

    private string CollectionPath => Path.Combine(_options.EffectiveCollectionBasePath, Name);

    private IZvecCollection GetOrOpenNativeCollection()
    {
        if (_nativeCollection != null) return _nativeCollection;

        lock (_initLock)
        {
            if (_nativeCollection != null) return _nativeCollection;

            _nativeCollection = OpenNativeCollection();
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
        if (doc == null || (_typeModel == null && _mapper == null)) return null;

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
        if (pkList.Count == 0 || (_typeModel == null && _mapper == null)) yield break;

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

        // Filter-only retrieval: ZVec requires a vector query to drive QueryAsync, but the
        // vector itself is irrelevant when a filter selects the rows. Use a zero-filled
        // vector sized to the collection's actual vector dimension (read from the type
        // model) so non-768-dim collections do not produce malformed queries.
        var firstVector = _typeModel?.Vectors.FirstOrDefault();
        string vectorFieldName = ResolveVectorFieldName();
        int vectorDimension = firstVector?.Dimension > 0
            ? firstVector.Dimension
            : Definition?.Properties.OfType<VectorStoreVectorProperty>().FirstOrDefault()?.Dimensions
                ?? ZVecConstants.DefaultVectorDimension;
        var dummyQuery = new ZVecQuery { FieldName = vectorFieldName, Vector = new float[vectorDimension] };
        var docs = await collection.QueryAsync(dummyQuery, effectiveTop, filterBuilder, includeVector: options?.IncludeVectors ?? true, ct: cancellationToken);

        if (_typeModel != null || _mapper != null)
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

        if (_typeModel == null && _mapper == null) return;
        var collection = GetOrOpenNativeCollection();
        var doc = MapToDoc(record);
        await collection.UpsertAsync(doc, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task UpsertAsync(IEnumerable<TRecord> records, CancellationToken cancellationToken = default)
    {
        if (records == null) throw new ArgumentNullException(nameof(records));
        cancellationToken.ThrowIfCancellationRequested();

        if (_typeModel == null && _mapper == null) return;
        var collection = GetOrOpenNativeCollection();
        var docs = records.Select(MapToDoc).ToList();
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

        ReadOnlyMemory<float> floatMemory;
        if (searchValue is ReadOnlyMemory<float> rom)
            floatMemory = rom;
        else if (searchValue is Memory<float> mem)
            floatMemory = mem;
        else if (searchValue is float[] arr)
            floatMemory = arr;
        else
            throw new NotSupportedException(ZVecErrorMessages.UnsupportedVectorType(typeof(TInput).Name));

        using var handle = floatMemory.Pin();
        cancellationToken.ThrowIfCancellationRequested();

        var collection = GetOrOpenNativeCollection();
        int effectiveTop = top > 0 ? top : ZVecConstants.DefaultQueryLimit;
        double scoreThreshold = options?.ScoreThreshold ?? ZVecConstants.DefaultMinScoreThreshold;

        string vectorFieldName = ResolveVectorFieldName(TryGetPropertyName(options?.VectorProperty));
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

        if (_typeModel != null || _mapper != null)
        {
            foreach (var doc in docs)
            {
                float similarityScore = NormalizeDenseScore(doc.Score);
                if (similarityScore >= scoreThreshold)
                {
                    var record = MapFromDoc(doc);
                    yield return new VectorSearchResult<TRecord>(record, similarityScore);
                }
            }
        }
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

        ReadOnlyMemory<float> floatMemory;
        if (searchValue is ReadOnlyMemory<float> rom)
            floatMemory = rom;
        else if (searchValue is Memory<float> mem)
            floatMemory = mem;
        else if (searchValue is float[] arr)
            floatMemory = arr;
        else
            throw new NotSupportedException(ZVecErrorMessages.UnsupportedVectorType(typeof(TInput).Name));

        using var handle = floatMemory.Pin();
        cancellationToken.ThrowIfCancellationRequested();

        var collection = GetOrOpenNativeCollection();
        int effectiveTop = top > 0 ? top : ZVecConstants.DefaultQueryLimit;
        double scoreThreshold = options?.ScoreThreshold ?? ZVecConstants.DefaultMinScoreThreshold;

        // Resolve the dense vector field. If the caller specified VectorProperty on the
        // options, honor it; otherwise fall back to the first (default) vector on the
        // type model.
        string? optionsVectorProperty = TryGetPropertyName(options?.VectorProperty);
        string vectorFieldName = !string.IsNullOrEmpty(optionsVectorProperty)
            ? optionsVectorProperty!
            : ResolveVectorFieldName();

        // Resolve the FTS field. Honor AdditionalProperty when supplied; otherwise pick
        // the first field marked [ZVecFullTextSearch] / IsFullTextIndexed on the type
        // model, falling back to the first scalar field only when no FTS field exists.
        string? optionsAdditionalProperty = TryGetPropertyName(options?.AdditionalProperty);
        string ftsFieldName = !string.IsNullOrEmpty(optionsAdditionalProperty)
            ? optionsAdditionalProperty!
            : ResolveFullTextField();

        string ftsQueryString = string.Join(" ", keywords);

        var denseQuery = new ZVecQuery { FieldName = vectorFieldName, Vector = floatMemory };
        var ftsQuery = new ZVecQuery { FieldName = ftsFieldName, Fts = new ZVecFtsQuery { QueryString = ftsQueryString } };

        // Tunable RRF: callers can pass ZVecHybridSearchOptions<TRecord> to override the
        // native rank constant; otherwise the default (k=60) is used.
        int rrfK = (options as ZVecHybridSearchOptions<TRecord>)?.RrfK ?? ZVecConstants.DefaultRrfRankConstant;
        var reranker = new ZVecRrfReranker { RankConstant = rrfK };

        IReadOnlyList<ZVecDoc> docs;
        if (options?.Filter != null)
        {
            var filterBuilder = ZVecFilterExpressionVisitor.TranslateToBuilder(options.Filter);
            docs = await collection.QueryAsync(new[] { denseQuery, ftsQuery }, effectiveTop, reranker, filterBuilder, includeVector: options?.IncludeVectors ?? true, ct: cancellationToken);
        }
        else
        {
            docs = await collection.QueryAsync(new[] { denseQuery, ftsQuery }, effectiveTop, reranker, filter: (string?)null, includeVector: options?.IncludeVectors ?? true, ct: cancellationToken);
        }

        if (_typeModel != null || _mapper != null)
        {
            foreach (var doc in docs)
            {
                // RRF fusion scores are already higher-is-better rank fusion values — do not re-normalize.
                float rrfScore = doc.Score;
                if (rrfScore >= scoreThreshold)
                {
                    var record = MapFromDoc(doc);
                    yield return new VectorSearchResult<TRecord>(record, rrfScore);
                }
            }
        }
    }

    /// <summary>
    /// Releases the native read-write collection handle without deleting on-disk data.
    /// </summary>
    /// <remarks>
    /// ZVec enforces a single read-write handle per collection path. Scoped RAG services call this
    /// when a DI scope ends so a subsequent scope can reopen the same collection.
    /// </remarks>
    public void ReleaseNativeHandle()
    {
        lock (_initLock)
        {
            if (_nativeCollection != null)
            {
                try { _nativeCollection.Dispose(); } catch { }
                _nativeCollection = null;
            }
        }
    }

    /// <summary>
    /// Optimizes the underlying native collection index and refreshes the internal collection handle.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// Call this method after batch ingestion to merge vector flat buffers into HNSW index segments
    /// and refresh the native collection handle for concurrent queriers.
    /// <para>
    /// ZVec enforces a single read-write handle per collection path, so the old handle MUST be
    /// disposed before the new one can be opened. The expensive native <c>OptimizeAsync</c> runs
    /// OUTSIDE the lock; the lock is held only for the minimal dispose-then-reopen window that
    /// ZVec's single-handle constraint requires. On reopen failure, <c>_nativeCollection</c> is
    /// cleared and subsequent operations recover lazily via <see cref="GetOrOpenNativeCollection"/>.
    /// </para>
    /// </remarks>
    public async Task OptimizeAndReopenAsync(CancellationToken cancellationToken = default)
    {
        var collection = GetOrOpenNativeCollection();
        await collection.OptimizeAsync(cancellationToken).ConfigureAwait(false);

        lock (_initLock)
        {
            var oldCollection = _nativeCollection;

            // ZVec requires the old read-write handle to be released before a new one can be
            // opened on the same collection path.
            if (oldCollection != null)
            {
                try { oldCollection.Dispose(); } catch { }
            }

            try
            {
                _nativeCollection = OpenNativeCollection();
            }
            catch
            {
                // Clear the disposed handle reference; lazy reopen in GetOrOpenNativeCollection()
                // recovers on the next access. The single-handle constraint prevents keeping the
                // old handle alive here, so recovery is via lazy reopen rather than handle retention.
                _nativeCollection = null;
                throw;
            }
        }
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
