using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData.Attributes;
using ZVec.Extensions.VectorData.Constants;
using ZVec.NET;
using ZVec.NET.Mapping;
using ZVec.NET.Query;

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
        Definition = definition;
        if (typeof(TRecord) != typeof(Dictionary<string, object?>))
        {
            _typeModel = ZVecTypeModel.Get<TRecord>();
            _mapper = ZVecRecordMapperRegistry.Get<TRecord>();
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

    private ZVecCollectionSchema BuildCollectionSchema()
    {
        var schemaBuilder = ZVecCollectionSchemaBuilder.From<TRecord>();
        var schema = schemaBuilder.Build();

        var ftsFieldNames = new HashSet<string>(StringComparer.Ordinal);
        var ftsVectors = new List<ZVecVectorSchema>(schema.Vectors);

        foreach (var field in schema.Fields)
        {
            if (field.DataType != ZVecDataType.String)
                continue;

            var prop = typeof(TRecord).GetProperty(field.Name);
            if (prop == null || !IsFullTextIndexedProperty(prop) || ftsVectors.Any(v => v.Name == field.Name))
                continue;

            ftsFieldNames.Add(field.Name);
            ftsVectors.Add(new ZVecVectorSchema
            {
                Name = field.Name,
                DataType = ZVecDataType.String,
                Dimension = 0,
                IndexParam = new ZVecFtsIndexParam()
            });
        }

        var updatedFields = schema.Fields.Where(f => !ftsFieldNames.Contains(f.Name)).ToArray();

        return new ZVecCollectionSchema
        {
            Name = schema.Name,
            MaxDocCountPerSegment = schema.MaxDocCountPerSegment,
            Fields = updatedFields,
            Vectors = ftsVectors.ToArray()
        };
    }

    /// <summary>
    /// Resolves whether a record property participates in full-text search indexing.
    /// </summary>
    /// <remarks>
    /// <c>[ZVecFullTextSearch]</c> takes precedence. <c>[VectorStoreData(IsFullTextIndexed = true)]</c>
    /// is recognized as a fallback when no ZVec FTS attribute is present.
    /// </remarks>
    private static bool IsFullTextIndexedProperty(PropertyInfo prop)
    {
        var zvecFtsAttr = (ZVecFullTextSearchAttribute?)Attribute.GetCustomAttribute(prop, typeof(ZVecFullTextSearchAttribute));
        if (zvecFtsAttr != null)
            return zvecFtsAttr.IsFullTextIndexed;

        var vectorDataAttr = (VectorStoreDataAttribute?)Attribute.GetCustomAttribute(prop, typeof(VectorStoreDataAttribute));
        return vectorDataAttr?.IsFullTextIndexed == true;
    }

    private IZvecCollection OpenNativeCollection()
    {
        if (!_factory.IsInitialized)
            _factory.Initialize();

        Directory.CreateDirectory(_options.EffectiveCollectionBasePath);
        return _factory.OpenOrCreate(CollectionPath, BuildCollectionSchema());
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
        var doc = MapToDoc(record);
        await collection.UpsertAsync(doc, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task UpsertAsync(IEnumerable<TRecord> records, CancellationToken cancellationToken = default)
    {
        if (records == null) throw new ArgumentNullException(nameof(records));
        cancellationToken.ThrowIfCancellationRequested();

        if (_typeModel == null) return;
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
                float similarityScore = NormalizeScore(doc.Score);
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

        string vectorFieldName = _typeModel?.Vectors.FirstOrDefault()?.StorageName ?? "Vector";
        string ftsFieldName = _typeModel?.Fields.FirstOrDefault()?.StorageName ?? "Content";
        string ftsQueryString = string.Join(" ", keywords);

        var denseQuery = new ZVecQuery { FieldName = vectorFieldName, Vector = floatMemory };
        var ftsQuery = new ZVecQuery { FieldName = ftsFieldName, Fts = new ZVecFtsQuery { QueryString = ftsQueryString } };
        var reranker = new ZVecRrfReranker();

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

        if (_typeModel != null)
        {
            foreach (var doc in docs)
            {
                float similarityScore = NormalizeScore(doc.Score);
                if (similarityScore >= scoreThreshold)
                {
                    var record = MapFromDoc(doc);
                    yield return new VectorSearchResult<TRecord>(record, similarityScore);
                }
            }
        }
    }

    /// <summary>
    /// Normalizes a native ZVec score into a similarity score where higher = better match.
    /// Switches on the configured <see cref="ZVecMetricType"/> for the collection.
    /// </summary>
    private float NormalizeScore(float nativeScore)
    {
        var indexParam = _typeModel?.Vectors.FirstOrDefault()?.IndexParam;
        ZVecMetricType metric = (indexParam as ZVecHnswIndexParam)?.MetricType ?? ZVecMetricType.Cosine;

        return metric switch
        {
            ZVecMetricType.Cosine => 1.0f - nativeScore,
            ZVecMetricType.L2 => 1.0f / (1.0f + nativeScore),
            ZVecMetricType.Ip => nativeScore,
            _ => 1.0f - nativeScore
        };
    }

    /// <summary>
    /// Optimizes the underlying native collection index and atomically updates the internal collection handle.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// Call this method after batch ingestion to merge vector flat buffers into HNSW index segments
    /// and atomically refresh the native collection handle for concurrent queriers.
    /// Expensive schema rebuild and native reopen occur outside the lock; only the handle swap is synchronized.
    /// </remarks>
    public async Task OptimizeAndReopenAsync(CancellationToken cancellationToken = default)
    {
        var collection = GetOrOpenNativeCollection();
        await collection.OptimizeAsync(cancellationToken).ConfigureAwait(false);

        IZvecCollection? toDispose;
        lock (_initLock)
        {
            toDispose = _nativeCollection;
            _nativeCollection = null;

            if (toDispose != null)
            {
                try { toDispose.Dispose(); } catch { }
            }

            _nativeCollection = OpenNativeCollection();
        }
    }

    [RequiresUnreferencedCode("Source generated mappers should be used for Native AOT. Reflection fallback may be trimmed.")]
    [RequiresDynamicCode("Reflection fallback requires dynamic code generation.")]
    private ZVecDoc MapToDoc(TRecord record)
    {
        if (_mapper != null)
        {
            return _mapper.ToDoc(record, _typeModel!);
        }
        return ZVecMapper.ToDoc(record, _typeModel!);
    }

    [RequiresUnreferencedCode("Source generated mappers should be used for Native AOT. Reflection fallback may be trimmed.")]
    [RequiresDynamicCode("Reflection fallback requires dynamic code generation.")]
    private TRecord MapFromDoc(ZVecDoc doc)
    {
        if (_typeModel == null) throw new InvalidOperationException("Type model is uninitialized.");

        if (_mapper != null)
        {
            return _mapper.FromDoc(doc, _typeModel);
        }

        // Reflection fallback — only used for Dictionary<string, object?> dynamic collections
        // or when SG mapper is not generated (e.g. during early development).
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
