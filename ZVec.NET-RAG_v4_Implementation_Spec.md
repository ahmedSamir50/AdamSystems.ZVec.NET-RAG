# ZVec.NET-RAG v4 — Implementation Spec (Zero-Invention, Review-Passing)

> **For the implementation agent.** Apply every patch in order. Do not invent code that is not written here. Do not skip verification steps. After all patches are applied, run the Final Verification Block at the end. The next review will pass **only if** every verification step succeeds.

> **Working directory for all relative paths**: the repository root (`AdamSystems.ZVec.NET-RAG/`).

> **Branch**: create `fix/v4-review-remediation` from `main` before applying patches.

---

## 0. Pre-flight inspection (mandatory — answers needed before patching)

The implementation depends on three facts about the ZVec.NET native API that this spec cannot infer. Run these inspections and record the answers; the patches below reference them by `[INSPECT-1]`, `[INSPECT-2]`, `[INSPECT-3]`.

### [INSPECT-1] — `IZvecCollection.QueryAsync` signature for hybrid search

In the ZVec.NET reference repo (`https://github.com/ahmedSamir50/AdamSystems.ZVec.NET`), open `src/ZVec.NET/Collections/IZvecCollection.cs` (or equivalent). Determine which of the following is true:

- **Option A**: `ZVecQuery` has a public settable property for FTS keywords (e.g. `Keywords`, `FtsQuery`, `TextQuery`).
- **Option B**: `IZvecCollection` exposes a separate method like `HybridQueryAsync(ZVecQuery, string ftsQuery, int top, ...)` or `QueryAsync(ZVecQuery, string keywords, ...)`.
- **Option C**: Neither — hybrid search must be done by issuing two separate queries (dense + FTS) and fusing with `ZVecRrfReranker` in managed code.

Record the answer. Patch P-03 below uses it.

### [INSPECT-2] — `ZVecMetricType` availability on collection schema

Open `src/ZVec.NET/Collections/ZVecCollectionSchema.cs` (or equivalent) and find where the metric type is stored. Determine whether:

- **Option A**: `IZvecCollection` exposes a `Metric` or `MetricType` property of type `ZVecMetricType`.
- **Option B**: The metric is part of `ZVecCollectionSchema` (e.g. `schema.Metric`).
- **Option C**: The metric is part of `ZVecTypeModel` (e.g. `model.Metric`).

Record the answer. Patch P-04 below uses it.

### [INSPECT-3] — `ZVecDoc.Score` semantics

Read the ZVec.NET docs or source to confirm: when a Cosine metric is configured, does `ZVecDoc.Score` return:

- **Option A**: cosine **distance** in `[0, 2]` (0 = identical, 2 = opposite) — needs `1.0f - distance` to convert to similarity in `[-1, 1]`
- **Option B**: cosine **distance** in `[0, 1]` (already normalized) — needs `1.0f - distance` to convert to similarity in `[0, 1]`
- **Option C**: cosine **similarity** directly in `[-1, 1]` — passthrough, no conversion

Record the answer. Patch P-04 below uses it.

---

## 1. Patch list (apply in order)

| ID | File | What it fixes |
|---|---|---|
| P-01 | `src/ZVec.Extensions.VectorData/ZVecVectorStoreOptions.cs` | StoragePath is settable + validated |
| P-02 | `src/ZVec.Extensions.VectorData/ZVecVectorStoreServiceCollectionExtensions.cs` | Thread StoragePath → ZVecFactory |
| P-03 | `src/ZVec.Extensions.VectorData/ZVecVectorizableRecordCollection.cs` | HybridSearch keywords; score normalization; StoragePath routing; consume SG mapper |
| P-04 | `src/ZVec.Extensions.VectorData/ZVecVectorStore.cs` | ListCollectionNames filtering; StoragePath routing |
| P-05 | `src/ZVec.Extensions.VectorData/IZVecRecordMapper.cs` (NEW) | Mapper interface for AOT-clean mapping |
| P-06 | `src/ZVec.Extensions.VectorData/ZVecRecordMapperRegistry.cs` (NEW) | Module-init registry |
| P-07 | `src/ZVec.Extensions.VectorData.SourceGenerator/ZVecRecordMetadataGenerator.cs` | Emit MapToDoc + MapFromDoc + ModuleInitializer registration |
| P-08 | `tests/ZVec.AotTestApp/Program.cs` | Expand AOT coverage: store, collection, filter, search |
| P-09 | `tests/ZVec.AotTestApp/ZVec.AotTestApp.csproj` | Fix `[DynamicallyAccessedMembers]` annotation target |
| P-10 | `tests/ZVec.Extensions.VectorData.Tests/ZVecHybridSearchTests.cs` | Honest hybrid round-trip test |
| P-11 | `tests/ZVec.Extensions.VectorData.Tests/ZVecVectorStoreTests.cs` | Fix 3 fake tests + add round-trip |
| P-12 | `tests/ZVec.Extensions.VectorData.Tests/ZVecVectorizableRecordCollectionTests.cs` | Fix tempDir leak |
| P-13 | `tests/ZVec.Extensions.VectorData.Tests/ZVecFilterExpressionVisitorTests.cs` | Add 5 missing tests |
| P-14 | `tests/ZVec.Extensions.VectorData.Tests/ZVecScoreNormalizationTests.cs` (NEW) | Score normalization tests |
| P-15 | `tests/ZVec.Extensions.VectorData.ConformanceTests/VectorStoreConformanceFixture.cs` | Rename file OR replace with real conformance test |
| P-16 | `README.md` | Phase 2 status banners |
| P-17 | `docs/architecture/rag-pipeline.md` | Phase 2 status banners |
| P-18 | `docs/architecture/security-threat-model.md` | Phase 2 status banners |
| P-19 | `docs/architecture/hybrid-search-rrf.md` | Phase 2 status banners |
| P-20 | `docs/architecture/interface-segregation.md` | Phase 2 status banners |
| P-21 | `docs/architecture/score-semantics.md` | Verify implementation matches doc |

---

## P-01 — `src/ZVec.Extensions.VectorData/ZVecVectorStoreOptions.cs`

**Replace the entire file with:**

```csharp
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
```

**Verification**: `dotnet build src/ZVec.Extensions.VectorData/ZVec.Extensions.VectorData.csproj` — 0 warnings.

---

## P-02 — `src/ZVec.Extensions.VectorData/ZVecVectorStoreServiceCollectionExtensions.cs`

**Replace the entire file with:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.VectorData;
using ZVec.NET;

namespace ZVec.Extensions.VectorData;

/// <summary>
/// Service collection extension methods for registering ZVec vector store services into DI containers.
/// </summary>
public static class ZVecVectorStoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="ZVecVectorStore"/> and <see cref="VectorStore"/> to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The target <see cref="IServiceCollection"/>.</param>
    /// <param name="configure">Optional configuration callback for <see cref="ZVecVectorStoreOptions"/>.</param>
    /// <param name="lifetime">The service lifetime for vector store registrations (defaults to <see cref="ServiceLifetime.Singleton"/>).</param>
    /// <returns>The modified <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <remarks>
    /// <b>Singleton lifetime is mandatory for <see cref="IZvecFactory"/>.</b> The native factory owns
    /// process-wide resources (file handles, mmap regions, P/Invoke SafeHandles) that must NOT be
    /// shared across multiple factory instances pointing at the same storage path.
    /// <see cref="ZVecVectorStore"/> is registered with the same lifetime as <paramref name="lifetime"/>.
    /// </remarks>
    public static IServiceCollection AddZVecVectorStore(
        this IServiceCollection services,
        Action<ZVecVectorStoreOptions>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        var options = new ZVecVectorStoreOptions();
        configure?.Invoke(options);

        services.TryAddSingleton<IZvecFactory>(sp =>
        {
            if (options.Factory != null)
            {
                if (!options.Factory.IsInitialized)
                {
                    options.Factory.Initialize();
                }
                return options.Factory;
            }

            var factory = new ZVecFactory();
            factory.Initialize();
            return factory;
        });

        // ZVecVectorStoreOptions registered as singleton so EffectiveCollectionBasePath
        // is computed once and shared by all ZVecVectorStore instances and collections.
        services.TryAddSingleton(options);

        var descriptor = ServiceDescriptor.Describe(
            typeof(ZVecVectorStore),
            sp => new ZVecVectorStore(
                sp.GetRequiredService<IZvecFactory>(),
                sp.GetRequiredService<ZVecVectorStoreOptions>()),
            lifetime);

        var abstractDescriptor = ServiceDescriptor.Describe(
            typeof(VectorStore),
            sp => sp.GetRequiredService<ZVecVectorStore>(),
            lifetime);

        services.TryAdd(descriptor);
        services.TryAdd(abstractDescriptor);

        return services;
    }
}
```

**Verification**:
1. Build succeeds with 0 warnings.
2. `services.AddZVecVectorStore(o => o.StoragePath = "/tmp/zvec-tests")` resolves `IZvecFactory` as singleton.
3. Resolving `ZVecVectorStore` twice returns the same instance when `lifetime = Singleton`.

---

## P-03 — `src/ZVec.Extensions.VectorData/ZVecVectorizableRecordCollection.cs`

This patch fixes four issues at once:
1. Hybrid search keywords actually passed to native engine (Gap I1)
2. Score normalization switched on `ZVecMetricType` (Gap I2)
3. `CollectionPath` uses `StoragePath` from options (Gap I3)
4. `MapFromDoc` uses SG-emitted mapper when available, falls back to reflection only for `Dictionary<string, object?>` (Gap I4)

**Constructor signature change**: takes `ZVecVectorStoreOptions` instead of just `IZvecFactory`.

**Replace the entire file with:**

```csharp
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.VectorData;
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

            if (!_factory.IsInitialized)
            {
                _factory.Initialize();
            }

            Directory.CreateDirectory(_options.EffectiveCollectionBasePath);
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
                    float similarityScore = NormalizeScore(doc.Score);
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
            double scoreThreshold = options?.ScoreThreshold ?? ZVecConstants.DefaultMinScoreThreshold;

            string vectorFieldName = _typeModel?.Vectors.FirstOrDefault()?.StorageName ?? "Vector";
            string ftsQuery = string.Join(" ", keywords);

            // === [INSPECT-1] — choose ONE branch based on inspection answer ===
            //
            // If Option A (ZVecQuery has Keywords/FtsQuery property):
            //     var query = new ZVecQuery { FieldName = vectorFieldName, Vector = floatMemory, FtsQuery = ftsQuery };
            //     docs = await collection.QueryAsync(query, effectiveTop, filterBuilder, includeVector: ..., ct: ...);
            //
            // If Option B (separate HybridQueryAsync method):
            //     var query = new ZVecQuery { FieldName = vectorFieldName, Vector = floatMemory };
            //     docs = await collection.HybridQueryAsync(query, ftsQuery, effectiveTop, filterBuilder, includeVector: ..., ct: ...);
            //
            // If Option C (two queries + managed RRF):
            //     var denseDocs = await collection.QueryAsync(denseQuery, effectiveTop, filterBuilder, ...);
            //     var ftsDocs = await collection.FtsQueryAsync(ftsQuery, effectiveTop, filterBuilder, ...);
            //     docs = ZVecRrfReranker.Fuse(denseDocs, ftsDocs, k: 60);
            //
            // === Default implementation below assumes Option A — adjust if inspection returned B or C ===

            var query = new ZVecQuery
            {
                FieldName = vectorFieldName,
                Vector = floatMemory
                // FtsQuery = ftsQuery  // <-- UNCOMMENT if ZVecQuery exposes this property (Option A)
            };

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
            yield break;
        }

        throw new NotSupportedException(ZVecErrorMessages.UnsupportedVectorType(typeof(TInput).Name));
    }

    /// <summary>
    /// Normalizes a native ZVec score into a similarity score where higher = better match.
    /// Switches on the configured <see cref="ZVecMetricType"/> for the collection.
    /// </summary>
    /// <remarks>
    /// Formula matrix (must match docs/architecture/score-semantics.md exactly):
    /// <list type="bullet">
    ///   <item>Cosine: <c>1.0f - distance</c> → similarity in [-1, 1]</item>
    ///   <item>L2:     <c>1.0f / (1.0f + distance)</c> → similarity in [0, 1]</item>
    ///   <item>InnerProduct: passthrough</item>
    ///   <item>Default (unknown metric): <c>1.0f - distance</c></item>
    /// </list>
    /// </remarks>
    private float NormalizeScore(float nativeScore)
    {
        // === [INSPECT-2] — retrieve metric from the right place ===
        // Adjust the line below to read the metric from the inspected location:
        //   Option A: _nativeCollection.Metric
        //   Option B: _nativeCollection.Schema.Metric
        //   Option C: _typeModel?.Metric ?? ZVecMetricType.Cosine
        ZVecMetricType metric = ZVecMetricType.Cosine; // <-- REPLACE with actual retrieval

        // === [INSPECT-3] — if ZVecDoc.Score already returns similarity (Option C), return nativeScore directly ===
        return metric switch
        {
            ZVecMetricType.Cosine => 1.0f - nativeScore,
            ZVecMetricType.L2 => 1.0f / (1.0f + nativeScore),
            ZVecMetricType.InnerProduct => nativeScore,
            _ => 1.0f - nativeScore
        };
    }

    private ZVecDoc MapToDoc(TRecord record)
    {
        if (_mapper != null)
        {
            return _mapper.ToDoc(record, _typeModel!);
        }
        return ZVecMapper.ToDoc(record, _typeModel!);
    }

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
```

**Critical follow-up steps**:
1. After [INSPECT-1] is resolved, uncomment / adjust the hybrid query construction in `HybridSearchAsync`.
2. After [INSPECT-2] is resolved, replace the placeholder `ZVecMetricType.Cosine` line with the actual metric retrieval.
3. After [INSPECT-3] is resolved, if native score is already similarity, simplify `NormalizeScore` to passthrough.

**Verification**:
1. `dotnet build src/ZVec.Extensions.VectorData/ZVec.Extensions.VectorData.csproj` — 0 warnings.
2. Confirm `MapFromDoc` uses `_mapper` when non-null (zero reflection path).
3. Confirm `CollectionPath` includes `_options.EffectiveCollectionBasePath`, not `AppDomain.CurrentDomain.BaseDirectory`.

---

## P-04 — `src/ZVec.Extensions.VectorData/ZVecVectorStore.cs`

**Replace the entire file with:**

```csharp
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
```

**Verification**:
1. Build succeeds with 0 warnings.
2. `ListCollectionNamesAsync` does NOT return `bin`, `obj`, `Debug`, `Release`, `net8.0`.
3. `CollectionExistsAsync` uses `_options.EffectiveCollectionBasePath`, not `AppDomain.CurrentDomain.BaseDirectory`.

---

## P-05 — `src/ZVec.Extensions.VectorData/IZVecRecordMapper.cs` (NEW FILE)

```csharp
using ZVec.NET;
using ZVec.NET.Mapping;

namespace ZVec.Extensions.VectorData;

/// <summary>
/// AOT-clean zero-reflection mapper between a POCO record type and a <see cref="ZVecDoc"/>.
/// Implementations are emitted by <c>ZVecRecordMetadataGenerator</c> for each
/// <c>[VectorStoreRecord]</c>-annotated class.
/// </summary>
/// <typeparam name="TRecord">The POCO record type.</typeparam>
public interface IZVecRecordMapper<TRecord> where TRecord : class
{
    /// <summary>
    /// Converts a POCO record into a <see cref="ZVecDoc"/> for native upsert.
    /// Zero reflection — direct property access.
    /// </summary>
    ZVecDoc ToDoc(TRecord record, ZVecTypeModel model);

    /// <summary>
    /// Converts a <see cref="ZVecDoc"/> back into a POCO record after native fetch.
    /// Zero reflection — direct property access.
    /// </summary>
    TRecord FromDoc(ZVecDoc doc, ZVecTypeModel model);
}
```

**Verification**: file exists, build succeeds with 0 warnings.

---

## P-06 — `src/ZVec.Extensions.VectorData/ZVecRecordMapperRegistry.cs` (NEW FILE)

```csharp
using System.Collections.Concurrent;
using ZVec.NET;

namespace ZVec.Extensions.VectorData;

/// <summary>
/// Process-wide registry of AOT-clean record mappers emitted by the source generator.
/// Mappers self-register via <c>[ModuleInitializer]</c> at assembly load time —
/// no runtime reflection, no <c>Activator.CreateInstance</c>.
/// </summary>
public static class ZVecRecordMapperRegistry
{
    private static readonly ConcurrentDictionary<Type, object> _mappers = new();

    /// <summary>
    /// Registers a mapper for type <typeparamref name="TRecord"/>. Called from
    /// <c>[ModuleInitializer]</c> methods emitted by the source generator.
    /// </summary>
    public static void Register<TRecord>(IZVecRecordMapper<TRecord> mapper) where TRecord : class
    {
        if (mapper == null) throw new ArgumentNullException(nameof(mapper));
        _mappers[typeof(TRecord)] = mapper;
    }

    /// <summary>
    /// Returns the registered mapper for <typeparamref name="TRecord"/>, or <c>null</c>
    /// if no mapper has been registered (e.g. for <c>Dictionary&lt;string, object?&gt;</c> dynamic collections).
    /// </summary>
    public static IZVecRecordMapper<TRecord>? Get<TRecord>() where TRecord : class
    {
        return _mappers.TryGetValue(typeof(TRecord), out var mapper)
            ? (IZVecRecordMapper<TRecord>)mapper
            : null;
    }
}
```

**Verification**: file exists, build succeeds with 0 warnings.

---

## P-07 — `src/ZVec.Extensions.VectorData.SourceGenerator/ZVecRecordMetadataGenerator.cs`

Replace the `GenerateSource` method body and add the new mapper emission logic. The generator now emits **both** the `VectorStoreCollectionDefinition` AND a `IZVecRecordMapper<TRecord>` implementation with `MapToDoc` / `MapFromDoc` methods, plus a `[ModuleInitializer]` registration.

**Replace the entire file with:**

```csharp
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ZVec.Extensions.VectorData.SourceGenerator;

/// <summary>
/// Incremental Roslyn Source Generator that produces zero-reflection static metadata mappers
/// for POCOs annotated with Microsoft.Extensions.VectorData attributes.
/// </summary>
/// <remarks>
/// <code>
/// ┌─────────────────────────────────────────────────────────────┐
/// │               Annotated [VectorStore] POCO                  │
/// ├─────────────────────────────────────────────────────────────┤
/// │            ZVecRecordMetadataGenerator (Roslyn SG)          │
/// ├─────────────────────────────────────────────────────────────┤
/// │   Emits &lt;Class&gt;ZVecMetadataMapper.g.cs (0-Reflection AOT)  │
/// │   • VectorStoreCollectionDefinition (key/vector/data props) │
/// │   • IZVecRecordMapper&lt;TRecord&gt; implementation              │
/// │   • [ModuleInitializer] auto-registration                   │
/// └─────────────────────────────────────────────────────────────┘
/// </code>
/// </remarks>
[Generator]
public sealed class ZVecRecordMetadataGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsCandidateClass(s),
                transform: static (ctx, _) => GetClassForGeneration(ctx))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(classDeclarations, static (spc, source) =>
        {
            if (source != null)
            {
                GenerateSource(spc, source.Value);
            }
        });
    }

    private static bool IsCandidateClass(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax classDecl) return false;
        return classDecl.AttributeLists
                   .SelectMany(al => al.Attributes)
                   .Any(a => a.Name.ToString().Contains("VectorStore"))
               || classDecl.Members.OfType<PropertyDeclarationSyntax>()
                   .Any(p => p.AttributeLists.SelectMany(al => al.Attributes)
                       .Any(a => a.Name.ToString().Contains("VectorStore")));
    }

    private static RecordModel? GetClassForGeneration(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

        if (symbol == null) return null;
        if (symbol.ContainingType != null) return null; // Skip nested classes
        if (symbol.ContainingNamespace.IsGlobalNamespace) return null;

        var properties = symbol.GetMembers().OfType<IPropertySymbol>().ToList();
        PropertyModel? keyProp = null;
        PropertyModel? vectorProp = null;
        int vectorDimensions = 0;
        var dataProps = new List<PropertyModel>();

        foreach (var p in properties)
        {
            foreach (var attr in p.GetAttributes())
            {
                string attrName = attr.AttributeClass?.Name ?? string.Empty;
                if (attrName.Contains("VectorStoreKey"))
                {
                    keyProp = new PropertyModel(p.Name, p.Type.ToDisplayString(), p.Type.SpecialType);
                }
                else if (attrName.Contains("VectorStoreVector"))
                {
                    vectorProp = new PropertyModel(p.Name, p.Type.ToDisplayString(), p.Type.SpecialType);
                    if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int dims)
                    {
                        vectorDimensions = dims;
                    }
                }
                else if (attrName.Contains("VectorStoreData"))
                {
                    dataProps.Add(new PropertyModel(p.Name, p.Type.ToDisplayString(), p.Type.SpecialType));
                }
            }
        }

        if (keyProp == null && vectorProp == null && dataProps.Count == 0)
            return null;

        string namespaceName = symbol.ContainingNamespace.ToDisplayString();
        string className = symbol.Name;

        return new RecordModel(
            namespaceName, className,
            keyProp, vectorProp, vectorDimensions, dataProps);
    }

    private static void GenerateSource(SourceProductionContext context, RecordModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using Microsoft.Extensions.VectorData;");
        sb.AppendLine("using ZVec.Extensions.VectorData;");
        sb.AppendLine("using ZVec.NET;");
        sb.AppendLine("using ZVec.NET.Mapping;");
        sb.AppendLine();
        sb.AppendLine($"namespace {model.NamespaceName};");
        sb.AppendLine();
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Generated zero-reflection static metadata mapper for <see cref=\"{model.ClassName}\"/>.");
        sb.AppendLine($"/// Emits VectorStoreCollectionDefinition, IZVecRecordMapper&lt;T&gt; implementation,");
        sb.AppendLine($"/// and ModuleInitializer registration.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public static class {model.ClassName}ZVecMetadataMapper");
        sb.AppendLine($"{{");
        sb.AppendLine($"    /// <summary>Generated collection definition.</summary>");
        sb.AppendLine($"    public static VectorStoreCollectionDefinition Definition {{ get; }} = new VectorStoreCollectionDefinition");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        Properties = new VectorStoreRecordProperty[]");
        sb.AppendLine($"        {{");

        if (model.KeyProp != null)
        {
            sb.AppendLine($"            new VectorStoreRecordKeyProperty(\"{model.KeyProp.Name}\", typeof({model.KeyProp.FullyQualifiedType})),");
        }
        if (model.VectorProp != null)
        {
            sb.AppendLine($"            new VectorStoreRecordVectorProperty(\"{model.VectorProp.Name}\", typeof({model.VectorProp.FullyQualifiedType}), {model.VectorDimensions}),");
        }
        foreach (var dataProp in model.DataProps)
        {
            sb.AppendLine($"            new VectorStoreRecordDataProperty(\"{dataProp.Name}\", typeof({dataProp.FullyQualifiedType})),");
        }

        sb.AppendLine($"        }}");
        sb.AppendLine($"    }};");
        sb.AppendLine();

        // === Emit IZVecRecordMapper<TRecord> implementation ===
        sb.AppendLine($"    /// <summary>Zero-reflection mapper for {model.ClassName}.</summary>");
        sb.AppendLine($"    public sealed class Mapper : IZVecRecordMapper<{model.ClassName}>");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        /// <inheritdoc />");
        sb.AppendLine($"        public ZVecDoc ToDoc({model.ClassName} record, ZVecTypeModel model)");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            if (record == null) throw new ArgumentNullException(nameof(record));");
        sb.AppendLine($"            if (model == null) throw new ArgumentNullException(nameof(model));");
        sb.AppendLine($"            var doc = new ZVecDoc();");
        if (model.KeyProp != null)
        {
            sb.AppendLine($"            doc.Id = record.{model.KeyProp.Name}?.ToString() ?? string.Empty;");
        }
        foreach (var dataProp in model.DataProps)
        {
            sb.AppendLine($"            doc.Fields[model.Fields.Find(f => f.PropertyName == \"{dataProp.Name}\")!.StorageName] = (object?)record.{dataProp.Name};");
        }
        if (model.VectorProp != null)
        {
            sb.AppendLine($"            var vecStorage = model.Vectors.Find(v => v.PropertyName == \"{model.VectorProp.Name}\")!.StorageName;");
            sb.AppendLine($"            doc.DenseVectors[vecStorage] = record.{model.VectorProp.Name};");
        }
        sb.AppendLine($"            return doc;");
        sb.AppendLine($"        }}");
        sb.AppendLine();
        sb.AppendLine($"        /// <inheritdoc />");
        sb.AppendLine($"        public {model.ClassName} FromDoc(ZVecDoc doc, ZVecTypeModel model)");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            if (doc == null) throw new ArgumentNullException(nameof(doc));");
        sb.AppendLine($"            if (model == null) throw new ArgumentNullException(nameof(model));");
        sb.AppendLine($"            var record = new {model.ClassName}();");
        if (model.KeyProp != null)
        {
            sb.AppendLine($"            record.{model.KeyProp.Name} = doc.Id;");
        }
        foreach (var dataProp in model.DataProps)
        {
            sb.AppendLine($"            var {dataProp.Name}Storage = model.Fields.Find(f => f.PropertyName == \"{dataProp.Name}\")!.StorageName;");
            sb.AppendLine($"            if (doc.Fields.TryGetValue({dataProp.Name}Storage, out var {dataProp.Name}Val) && {dataProp.Name}Val != null)");
            sb.AppendLine($"                record.{dataProp.Name} = ({dataProp.FullyQualifiedType}){dataProp.Name}Val;");
        }
        if (model.VectorProp != null)
        {
            sb.AppendLine($"            var {model.VectorProp.Name}Storage = model.Vectors.Find(v => v.PropertyName == \"{model.VectorProp.Name}\")!.StorageName;");
            sb.AppendLine($"            if (doc.DenseVectors.TryGetValue({model.VectorProp.Name}Storage, out var {model.VectorProp.Name}Dense))");
            sb.AppendLine($"                record.{model.VectorProp.Name} = {model.VectorProp.Name}Dense;");
        }
        sb.AppendLine($"            return record;");
        sb.AppendLine($"        }}");
        sb.AppendLine($"    }}");
        sb.AppendLine();

        // === ModuleInitializer registration ===
        sb.AppendLine($"    internal static class {model.ClassName}MapperRegistration");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        [ModuleInitializer]");
        sb.AppendLine($"        internal static void Register()");
        sb.AppendLine($"        {{");
        sb.AppendLine($"            ZVecRecordMapperRegistry.Register<{model.ClassName}>(new Mapper());");
        sb.AppendLine($"        }}");
        sb.AppendLine($"    }}");
        sb.AppendLine($"}}");

        string hintName = $"{model.NamespaceName.Replace('.', '_')}_{model.ClassName}ZVecMetadataMapper.g.cs";
        context.AddSource(hintName, SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private readonly struct PropertyModel
    {
        public PropertyModel(string name, string fullyQualifiedType, SpecialType specialType)
        {
            Name = name;
            FullyQualifiedType = fullyQualifiedType;
            SpecialType = specialType;
        }
        public string Name { get; }
        public string FullyQualifiedType { get; }
        public SpecialType SpecialType { get; }
    }

    private readonly struct RecordModel
    {
        public RecordModel(
            string namespaceName,
            string className,
            PropertyModel? keyPropName,
            PropertyModel? vectorPropName,
            int vectorDimensions,
            IReadOnlyList<PropertyModel> dataPropNames)
        {
            NamespaceName = namespaceName;
            ClassName = className;
            KeyProp = keyPropName;
            VectorProp = vectorPropName;
            VectorDimensions = vectorDimensions;
            DataProps = dataPropNames;
        }

        public string NamespaceName { get; }
        public string ClassName { get; }
        public PropertyModel? KeyProp { get; }
        public PropertyModel? VectorProp { get; }
        public int VectorDimensions { get; }
        public IReadOnlyList<PropertyModel> DataProps { get; }
    }
}
```

**Verification**:
1. Build the source generator project — 0 warnings.
2. Build `ZVec.Extensions.VectorData` — generated mappers must compile cleanly.
3. `MapToDoc` and `MapFromDoc` use direct property access, not `SetValue`/`GetValue`.
4. `[ModuleInitializer]` registration is emitted for every annotated class.

---

## P-08 — `tests/ZVec.AotTestApp/Program.cs`

**Replace the entire file with:**

```csharp
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.VectorData;
using ZVec.Extensions.VectorData;
using ZVec.NET.Mapping;

namespace ZVec.AotTestApp;

/// <summary>
/// Sample document model for Native AOT trim verification.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
public sealed class SampleAotDoc
{
    /// <summary>Unique Identifier.</summary>
    [ZVecId]
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>Dense embedding vector.</summary>
    [ZVecVector(768)]
    [VectorStoreVector(768)]
    public ReadOnlyMemory<float> Vector { get; set; }

    /// <summary>Sample title field.</summary>
    [ZVecField]
    [VectorStoreData(IsIndexed = true, IsFullTextIndexed = true)]
    public string Title { get; set; } = string.Empty;
}

public static class Program
{
    public static int Main()
    {
        Console.WriteLine("=== ZVec.NET Native AOT Audit Harness Starting ===");

        try
        {
            // Test 1: TypeModel Resolution under AOT
            var model = ZVecTypeModel.Get<SampleAotDoc>();
            Console.WriteLine($"[AOT Test 1] Model resolved: {model.ClrType.Name} (Id: {model.Id.Property.Name}, Fields: {model.Fields.Count}, Vectors: {model.Vectors.Count})");

            // Test 2: POCO to ZVecDoc Conversion & Vector Pinning under AOT
            float[] sampleVector = new float[768];
            sampleVector[0] = 0.42f;

            var record = new SampleAotDoc
            {
                Id = "doc_aot_001",
                Title = "AOT Document Test",
                Vector = sampleVector
            };

            var doc = ZVecMapper.ToDoc(record, model);
            Console.WriteLine($"[AOT Test 2] ZVecDoc created successfully. Id: {doc.Id}, Fields Count: {doc.Fields.Count}");

            // Test 3: Reverse ZVecDoc to POCO Mapping under AOT
            var restored = ZVecMapper.FromDoc<SampleAotDoc>(doc, model);
            Console.WriteLine($"[AOT Test 3] Document restored: Id={restored.Id}, Title={restored.Title}, VectorDim={restored.Vector.Length}");

            // Test 4: ZVecVectorStore instantiation + collection retrieval under AOT
            var options = new ZVecVectorStoreOptions
            {
                StoragePath = Path.Combine(Path.GetTempPath(), "ZVecAotTests", Guid.NewGuid().ToString("N"))
            };
            Directory.CreateDirectory(options.StoragePath);

            var store = new ZVecVectorStore(new ZVecFactory(), options);
            var collection = store.GetCollection<string, SampleAotDoc>("aot_test_collection");
            Console.WriteLine($"[AOT Test 4] ZVecVectorStore + collection resolved: {collection.Name}");

            // Test 5: Filter Expression Translation under AOT (no Expression.Compile)
            System.Linq.Expressions.Expression<Func<SampleAotDoc, bool>> filter = x => x.Title == "AOT Document Test";
            string filterStr = ZVecFilterExpressionVisitor.Translate(filter);
            Console.WriteLine($"[AOT Test 5] Filter translated: {filterStr}");

            // Test 6: Upsert + Search round-trip under AOT (verifies zero-reflection mapper)
            collection.EnsureCollectionExistsAsync(CancellationToken.None).GetAwaiter().GetResult();
            collection.UpsertAsync(record, CancellationToken.None).GetAwaiter().GetResult();

            var fetched = collection.GetAsync("doc_aot_001", cancellationToken: CancellationToken.None).GetAwaiter().GetResult();
            if (fetched == null) throw new InvalidOperationException("Fetched document was null after upsert.");
            Console.WriteLine($"[AOT Test 6] Upsert + Get round-trip OK. Fetched Title={fetched.Title}");

            // Test 7: Vectorized Search under AOT
            var searchResults = new List<VectorSearchResult<SampleAotDoc>>();
            var searchAsync = collection.SearchAsync(record.Vector, 5, cancellationToken: CancellationToken.None);
            var enumerator = searchAsync.GetAsyncEnumerator(CancellationToken.None);
            while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
            {
                searchResults.Add(enumerator.Current);
            }
            if (searchResults.Count == 0) throw new InvalidOperationException("Search returned no results under AOT.");
            Console.WriteLine($"[AOT Test 7] Vectorized search returned {searchResults.Count} result(s). Top score: {searchResults[0].Score}");

            // Cleanup
            collection.EnsureCollectionDeletedAsync(CancellationToken.None).GetAwaiter().GetResult();
            try { Directory.Delete(options.StoragePath, recursive: true); } catch { }

            Console.WriteLine("=== All Native AOT Verification Tests Passed Successfully ===");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] AOT Verification Failure: {ex.GetType().Name} - {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}
```

**Verification**:
1. `dotnet publish tests/ZVec.AotTestApp/ZVec.AotTestApp.csproj -c Release -r win-x64` — 0 IL2026 / IL3050 warnings.
2. Run the published binary — exit code 0, all 7 tests print success.
3. Run via WSL: `dotnet publish ... -r linux-x64` — 0 warnings.

---

## P-09 — `tests/ZVec.AotTestApp/ZVec.AotTestApp.csproj`

**Replace the entire file with:**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFrameworks></TargetFrameworks>
    <TargetFramework>net8.0</TargetFramework>
    <PublishAot>true</PublishAot>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
    <!-- During annotation transition, allow IL2007/IL2008 (benign doc-XML warnings) -->
    <!-- Remove this line once all [DynamicallyAccessedMembers] annotations are complete -->
    <WarningsNotAsErrors>IL2007;IL2008</WarningsNotAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ZVec.NET" />
    <PackageReference Include="Microsoft.Extensions.VectorData.Abstractions" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\ZVec.Extensions.VectorData\ZVec.Extensions.VectorData.csproj" />
  </ItemGroup>

</Project>
```

**Verification**: build succeeds with 0 warnings.

---

## P-10 — `tests/ZVec.Extensions.VectorData.Tests/ZVecHybridSearchTests.cs`

**Replace the entire file with:**

```csharp
using Microsoft.Extensions.VectorData;
using ZVec.NET;
using ZVec.NET.Mapping;
using Xunit;

namespace ZVec.Extensions.VectorData.Tests;

/// <summary>
/// Sample record type for Hybrid Search TDD unit tests.
/// </summary>
public sealed class SampleHybridRecord
{
    /// <summary>Document Key.</summary>
    [ZVecId]
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>Text Payload Field.</summary>
    [ZVecField]
    [VectorStoreData(IsIndexed = true, IsFullTextIndexed = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Embedding Vector Field.</summary>
    [ZVecVector(768)]
    [VectorStoreVector(768)]
    public ReadOnlyMemory<float> Vector { get; set; }
}

/// <summary>
/// TDD Unit tests verifying IKeywordHybridSearchable implementation in ZVecVectorizableRecordCollection.
/// All tests use isolated temp directories and round-trip real data — no stubs.
/// </summary>
public sealed class ZVecHybridSearchTests
{
    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "ZVecTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task HybridSearchAsync_ThrowsArgumentNullException_WhenSearchValueIsNull()
    {
        var options = new ZVecVectorStoreOptions { StoragePath = CreateTempStoragePath() };
        IZvecFactory factory = new ZVecFactory();
        IKeywordHybridSearchable<SampleHybridRecord> collection =
            new ZVecVectorizableRecordCollection<SampleHybridRecord, string>(factory, options, "hybrid_docs");

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var item in collection.HybridSearchAsync<string>(
                null!, new[] { "keyword" }, 10, cancellationToken: TestContext.Current.CancellationToken))
            {
                _ = item;
            }
        });
    }

    [Fact]
    public async Task HybridSearchAsync_ThrowsArgumentNullException_WhenKeywordsIsNull()
    {
        var options = new ZVecVectorStoreOptions { StoragePath = CreateTempStoragePath() };
        IZvecFactory factory = new ZVecFactory();
        IKeywordHybridSearchable<SampleHybridRecord> collection =
            new ZVecVectorizableRecordCollection<SampleHybridRecord, string>(factory, options, "hybrid_docs");

        ReadOnlyMemory<float> vector = new float[768];
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var item in collection.HybridSearchAsync(
                vector, null!, 10, cancellationToken: TestContext.Current.CancellationToken))
            {
                _ = item;
            }
        });
    }

    [Fact]
    public async Task HybridSearchAsync_ThrowsNotSupportedException_WhenSearchValueIsNotFloatMemory()
    {
        var options = new ZVecVectorStoreOptions { StoragePath = CreateTempStoragePath() };
        IZvecFactory factory = new ZVecFactory();
        IKeywordHybridSearchable<SampleHybridRecord> collection =
            new ZVecVectorizableRecordCollection<SampleHybridRecord, string>(factory, options, "hybrid_docs");

        double[] invalidVector = new double[768];
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await foreach (var item in collection.HybridSearchAsync(
                invalidVector, new[] { "keyword" }, 10, cancellationToken: TestContext.Current.CancellationToken))
            {
                _ = item;
            }
        });
    }

    [Fact]
    public async Task HybridSearchAsync_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        var options = new ZVecVectorStoreOptions { StoragePath = CreateTempStoragePath() };
        IZvecFactory factory = new ZVecFactory();
        IKeywordHybridSearchable<SampleHybridRecord> collection =
            new ZVecVectorizableRecordCollection<SampleHybridRecord, string>(factory, options, "hybrid_docs");

        ReadOnlyMemory<float> vector = new float[768];
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in collection.HybridSearchAsync(
                vector, new[] { "keyword" }, 10, cancellationToken: cts.Token))
            {
                _ = item;
            }
        });
    }

    /// <summary>
    /// HONEST HYBRID ROUND-TRIP TEST:
    /// Seeds real records with FTS-indexed text, executes HybridSearchAsync with vector + keywords,
    /// asserts non-empty results, and verifies that the record matching the keyword is returned.
    /// This test replaces the previous "Assert.Empty(results)" stub-state assertion.
    /// </summary>
    [Fact]
    public async Task HybridSearchAsync_ReturnsNonEmptyResults_WhenSeededWithRealRecordsAndKeywords()
    {
        string storagePath = CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);
        try
        {
            var options = new ZVecVectorStoreOptions { StoragePath = storagePath };
            IZvecFactory factory = new ZVecFactory();
            factory.Initialize();

            string colName = "hybrid_test_" + Guid.NewGuid().ToString("N")[..8];
            var collection = new ZVecVectorizableRecordCollection<SampleHybridRecord, string>(factory, options, colName);
            await collection.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);

            // Seed records with distinct content
            var vector1 = new float[768]; vector1[0] = 1.0f;
            var vector2 = new float[768]; vector2[0] = 0.8f;
            var vector3 = new float[768]; vector3[0] = 0.6f;

            await collection.UpsertAsync(new[]
            {
                new SampleHybridRecord { Id = "doc1", Content = "machine learning vector embeddings", Vector = vector1 },
                new SampleHybridRecord { Id = "doc2", Content = "neural network architecture", Vector = vector2 },
                new SampleHybridRecord { Id = "doc3", Content = "document retrieval keyword search", Vector = vector3 }
            }, TestContext.Current.CancellationToken);

            // Execute hybrid search with vector + keyword that matches doc3's content
            var queryVector = new float[768]; queryVector[0] = 0.7f;
            var keywords = new[] { "keyword", "retrieval" };

            var results = new List<VectorSearchResult<SampleHybridRecord>>();
            IKeywordHybridSearchable<SampleHybridRecord> hybrid = collection;
            await foreach (var res in hybrid.HybridSearchAsync(
                queryVector, keywords, 10, cancellationToken: TestContext.Current.CancellationToken))
            {
                results.Add(res);
            }

            // Assert non-empty results — implementation must actually execute the query
            Assert.NotEmpty(results);

            // Assert the keyword-matching document appears in results
            Assert.Contains(results, r => r.Record.Id == "doc3");

            await collection.EnsureCollectionDeletedAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            if (Directory.Exists(storagePath))
            {
                try { Directory.Delete(storagePath, recursive: true); } catch { }
            }
        }
    }
}
```

**Verification**:
1. `dotnet test tests/ZVec.Extensions.VectorData.Tests/ZVec.Extensions.VectorData.Tests.csproj --filter "FullyQualifiedName~ZVecHybridSearchTests"` — all tests pass.
2. `HybridSearchAsync_ReturnsNonEmptyResults_WhenSeededWithRealRecordsAndKeywords` must NOT contain `Assert.Empty` anywhere.

---

## P-11 — `tests/ZVec.Extensions.VectorData.Tests/ZVecVectorStoreTests.cs`

**Replace the entire file with:**

```csharp
using Microsoft.Extensions.VectorData;
using ZVec.NET;
using ZVec.NET.Mapping;
using Xunit;

namespace ZVec.Extensions.VectorData.Tests;

/// <summary>
/// Sample record type for ZVecVectorStore TDD tests.
/// </summary>
public sealed class TestStoreRecord
{
    /// <summary>Document Key.</summary>
    [ZVecId]
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>Title Data Field.</summary>
    [ZVecField]
    [VectorStoreData]
    public string Title { get; set; } = string.Empty;

    /// <summary>Embedding Vector Field.</summary>
    [ZVecVector(768)]
    [VectorStoreVector(768)]
    public ReadOnlyMemory<float> Vector { get; set; }
}

/// <summary>
/// Unit test suite for ZVecVectorStore (VectorStore implementation).
/// All tests use isolated temp directories — nofake `Assert.False` coincidences.
/// </summary>
public sealed class ZVecVectorStoreTests
{
    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "ZVecTests", Guid.NewGuid().ToString("N"));

    private static ZVecVectorStoreOptions CreateOptions(string? storagePath = null)
        => new() { StoragePath = storagePath ?? CreateTempStoragePath() };

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenZVecFactoryIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ZVecVectorStore(null!, CreateOptions()));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
    {
        IZvecFactory factory = new ZVecFactory();
        Assert.Throws<ArgumentNullException>(() => new ZVecVectorStore(factory, null!));
    }

    [Fact]
    public void GetCollection_ReturnsValidCollectionInstance_WhenParametersAreValid()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());

        var collection = store.GetCollection<string, TestStoreRecord>("test_store_records");

        Assert.NotNull(collection);
        Assert.Equal("test_store_records", collection.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetCollection_ThrowsArgumentException_WhenCollectionNameIsNullOrEmpty(string? invalidName)
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());

        Assert.Throws<ArgumentException>(() => store.GetCollection<string, TestStoreRecord>(invalidName!));
    }

    [Fact]
    public void GetCollection_PropagatesDefinition_WhenCustomDefinitionProvided()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());
        var customDefinition = new VectorStoreCollectionDefinition();

        var collection = store.GetCollection<string, TestStoreRecord>("test_store_records", customDefinition);

        Assert.NotNull(collection);
        Assert.Equal("test_store_records", collection.Name);
        var typedCollection = Assert.IsType<ZVecVectorizableRecordCollection<TestStoreRecord, string>>(collection);
        Assert.Same(customDefinition, typedCollection.Definition);
    }

    [Fact]
    public void GetDynamicCollection_ReturnsCollection_WhenParametersAreValid()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());
        var definition = new VectorStoreCollectionDefinition();

        var collection = store.GetDynamicCollection("test_store_records", definition);

        Assert.NotNull(collection);
        Assert.Equal("test_store_records", collection.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetDynamicCollection_ThrowsArgumentException_WhenNameInvalid(string? invalidName)
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());
        var definition = new VectorStoreCollectionDefinition();

        Assert.Throws<ArgumentException>(() => store.GetDynamicCollection(invalidName!, definition));
    }

    /// <summary>
    /// HONEST ROUND-TRIP: EnsureCollectionExistsAsync → CollectionExistsAsync == true →
    /// EnsureCollectionDeletedAsync → CollectionExistsAsync == false.
    /// Replaces the previous "Assert.False(exists)" stub assertion that only passed
    /// because no collection was ever created.
    /// </summary>
    [Fact]
    public async Task CollectionExistsAsync_ReturnsTrue_AfterEnsureCollectionExistsAsync_AndFalse_AfterEnsureCollectionDeletedAsync()
    {
        string storagePath = CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);
        try
        {
            IZvecFactory factory = new ZVecFactory();
            var store = new ZVecVectorStore(factory, CreateOptions(storagePath));
            string collectionName = "lifecycle_" + Guid.NewGuid().ToString("N")[..8];

            // Initially does not exist
            bool existsBefore = await store.CollectionExistsAsync(collectionName, TestContext.Current.CancellationToken);
            Assert.False(existsBefore);

            // After EnsureCollectionExistsAsync, must exist
            await store.GetCollection<string, TestStoreRecord>(collectionName)
                       .EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);
            bool existsAfterCreate = await store.CollectionExistsAsync(collectionName, TestContext.Current.CancellationToken);
            Assert.True(existsAfterCreate);

            // After EnsureCollectionDeletedAsync, must not exist
            await store.EnsureCollectionDeletedAsync(collectionName, TestContext.Current.CancellationToken);
            bool existsAfterDelete = await store.CollectionExistsAsync(collectionName, TestContext.Current.CancellationToken);
            Assert.False(existsAfterDelete);
        }
        finally
        {
            if (Directory.Exists(storagePath))
            {
                try { Directory.Delete(storagePath, recursive: true); } catch { }
            }
        }
    }

    /// <summary>
    /// HONEST ROUND-TRIP: ListCollectionNamesAsync returns names of actually-created collections.
    /// Verifies that a created collection appears in enumeration, and that excluded
    /// infrastructure directories (bin/obj/etc.) do NOT appear.
    /// </summary>
    [Fact]
    public async Task ListCollectionNamesAsync_ReturnsCreatedCollection_AndExcludesInfrastructureDirectories()
    {
        string storagePath = CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);
        try
        {
            // Create a "bin" directory to verify exclusion
            Directory.CreateDirectory(Path.Combine(storagePath, "bin"));
            Directory.CreateDirectory(Path.Combine(storagePath, "obj"));

            IZvecFactory factory = new ZVecFactory();
            var store = new ZVecVectorStore(factory, CreateOptions(storagePath));
            string collectionName = "listed_" + Guid.NewGuid().ToString("N")[..8];

            await store.GetCollection<string, TestStoreRecord>(collectionName)
                       .EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);

            var names = new List<string>();
            await foreach (var name in store.ListCollectionNamesAsync(TestContext.Current.CancellationToken))
            {
                names.Add(name);
            }

            Assert.Contains(collectionName, names);
            Assert.DoesNotContain("bin", names);
            Assert.DoesNotContain("obj", names);
        }
        finally
        {
            if (Directory.Exists(storagePath))
            {
                try { Directory.Delete(storagePath, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public void GetService_ReturnsFactory_WhenRequestedTypeIsIZvecFactory()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());

        object? service = store.GetService(typeof(IZvecFactory));

        Assert.Same(factory, service);
    }

    [Fact]
    public void GetService_ReturnsNull_WhenRequestedTypeIsUnknown()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());

        object? service = store.GetService(typeof(string));

        Assert.Null(service);
    }

    [Fact]
    public async Task CollectionExistsAsync_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.CollectionExistsAsync("test_store_records", cts.Token));
    }

    [Fact]
    public async Task EnsureCollectionDeletedAsync_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.EnsureCollectionDeletedAsync("test_store_records", cts.Token));
    }

    [Fact]
    public async Task ListCollectionNamesAsync_ThrowsOperationCanceledException_WhenCancellationTokenCanceled()
    {
        IZvecFactory factory = new ZVecFactory();
        var store = new ZVecVectorStore(factory, CreateOptions());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var name in store.ListCollectionNamesAsync(cts.Token))
            {
                _ = name;
            }
        });
    }
}
```

**Verification**:
1. `dotnet test --filter "FullyQualifiedName~ZVecVectorStoreTests"` — all tests pass.
2. No `Assert.False(exists)` test that only passes due to absence of setup.
3. No `Assert.NotNull(names)` trivial assertions on a `new List<string>()`.
4. The deleted `EnsureCollectionDeletedAsync_CompletesSuccessfully_WhenInvoked` (no-assertion) test is gone.

---

## P-12 — `tests/ZVec.Extensions.VectorData.Tests/ZVecVectorizableRecordCollectionTests.cs`

Fix the `UpsertAndGet_RoundTrip` test so `tempDir` is actually passed to the factory via `ZVecVectorStoreOptions.StoragePath`. Replace ONLY the round-trip test method (lines ~173-231 in the existing file). All other tests in this file remain valid.

**Find this block:**

```csharp
    [Fact]
    public async Task UpsertAndGet_RoundTrip_ReturnsUpsertedRecord_AndSearchFindsIt()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ZVecTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            IZvecFactory factory = new ZVecFactory();
            factory.Initialize();

            string colName = "test_roundtrip_" + Guid.NewGuid().ToString("N")[..8];
            var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, colName);

            await collection.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);
            bool exists = await collection.CollectionExistsAsync(TestContext.Current.CancellationToken);
            Assert.True(exists);
            ...
```

**Replace with:**

```csharp
    [Fact]
    public async Task UpsertAndGet_RoundTrip_ReturnsUpsertedRecord_AndSearchFindsIt()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ZVecTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = new ZVecVectorStoreOptions { StoragePath = tempDir };
            IZvecFactory factory = new ZVecFactory();
            factory.Initialize();

            string colName = "test_roundtrip_" + Guid.NewGuid().ToString("N")[..8];
            var collection = new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, options, colName);

            await collection.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);
            bool exists = await collection.CollectionExistsAsync(TestContext.Current.CancellationToken);
            Assert.True(exists);

            // Verify collection files actually live in tempDir, not in bin/
            string expectedCollectionPath = Path.Combine(tempDir, colName);
            Assert.True(Directory.Exists(expectedCollectionPath),
                $"Collection directory should exist at {expectedCollectionPath}, not in bin/.");

            var floatArray = new float[768];
            floatArray[0] = 1.0f;
            floatArray[1] = 0.5f;

            var record = new SampleCollectionRecord
            {
                Id = "doc1",
                Title = "TDD Real Vector Search Doc",
                Vector = floatArray
            };

            await collection.UpsertAsync(record, TestContext.Current.CancellationToken);

            var retrieved = await collection.GetAsync("doc1", cancellationToken: TestContext.Current.CancellationToken);
            Assert.NotNull(retrieved);
            Assert.Equal("doc1", retrieved.Id);
            Assert.Equal("TDD Real Vector Search Doc", retrieved.Title);

            var searchResults = new List<VectorSearchResult<SampleCollectionRecord>>();
            await foreach (var res in collection.SearchAsync(record.Vector, 10, cancellationToken: TestContext.Current.CancellationToken))
            {
                searchResults.Add(res);
            }

            Assert.NotEmpty(searchResults);
            Assert.Equal("doc1", searchResults[0].Record.Id);

            await collection.DeleteAsync("doc1", TestContext.Current.CancellationToken);
            var deletedDoc = await collection.GetAsync("doc1", cancellationToken: TestContext.Current.CancellationToken);
            Assert.Null(deletedDoc);

            await collection.EnsureCollectionDeletedAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }
```

**Also update all other tests in this file** that construct `ZVecVectorizableRecordCollection`:

Every occurrence of:
```csharp
new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName)
```
Must become:
```csharp
new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, new ZVecVectorStoreOptions(), TestCollectionName)
```

And:
```csharp
new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, TestCollectionName, customDefinition)
```
Must become:
```csharp
new ZVecVectorizableRecordCollection<SampleCollectionRecord, string>(factory, new ZVecVectorStoreOptions(), TestCollectionName, customDefinition)
```

And:
```csharp
new ZVecVectorizableRecordCollection<Dictionary<string, object?>, object>(factory, "dynamic_docs", definition)
```
Must become:
```csharp
new ZVecVectorizableRecordCollection<Dictionary<string, object?>, object>(factory, new ZVecVectorStoreOptions(), "dynamic_docs", definition)
```

**Verification**:
1. All tests pass.
2. After running tests, no `test_roundtrip_*` directories remain under `bin/`.
3. `Directory.Exists(expectedCollectionPath)` assertion proves files went to tempDir.

---

## P-13 — `tests/ZVec.Extensions.VectorData.Tests/ZVecFilterExpressionVisitorTests.cs`

Add 5 new test methods at the end of the `ZVecFilterExpressionVisitorTests` class (before the closing `}`). Do not modify existing tests.

**Append:**

```csharp
    // -------------------------------------------------------------------------
    // v4 Review Remediation: 5 missing filter visitor tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies string values containing double quotes are properly escaped in the
    /// generated filter string. Prevents SQL-injection-style filter breakage.
    /// </summary>
    [Fact]
    public void Translate_EqualOperator_EscapesDoubleQuotesInStringValue()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Category == "Evil\"OR";
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        // The double quote must be escaped (either as \" or "") — pick whichever ZVecFilterBuilder emits
        Assert.Contains("Evil\\\"OR", result);
    }

    /// <summary>
    /// Verifies that integer arrays in IN clauses emit unquoted numeric literals
    /// (not quoted string literals like "1", "2").
    /// </summary>
    [Fact]
    public void Translate_ContainsAny_NumericArray_EmitsUnquotedNumericLiterals()
    {
        int[] prices = new[] { 10, 20, 30 };
        // Note: this requires a numeric field on the POCO. Use Price.
        // Since Enumerable.Contains on int requires T == int and Price is int,
        // we test the inverse: filter where Price is in a numeric set.
        // The visitor must emit "Price IN (10, 20, 30)" not "Price IN (\"10\", \"20\", \"30\")"
        Expression<Func<FilterTestRecord, bool>> filter = x => prices.Contains(x.Price);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Price IN", result);
        Assert.Contains("10", result);
        Assert.Contains("20", result);
        Assert.Contains("30", result);
        // Must NOT contain quoted numbers
        Assert.DoesNotContain("\"10\"", result);
        Assert.DoesNotContain("\"20\"", result);
        Assert.DoesNotContain("\"30\"", result);
    }

    /// <summary>
    /// Verifies that an IN clause containing both null and non-null elements
    /// generates "(Property IN (...) OR Property IS NULL)".
    /// </summary>
    [Fact]
    public void Translate_ContainsAny_MixedNullAndNonNullElements_GeneratesInClauseWithIsNullAlternative()
    {
        string[] categories = new string?[] { "Electronics", null, "Books" }!;
        Expression<Func<FilterTestRecord, bool>> filter = x => categories.Contains(x.Category);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Category IN", result);
        Assert.Contains("\"Electronics\"", result);
        Assert.Contains("\"Books\"", result);
        // Must contain an IsNull alternative for the null element
        Assert.Contains("IS NULL", result.ToUpperInvariant());
    }

    /// <summary>
    /// Verifies that comparing a property to null translates to an IS NULL check.
    /// </summary>
    [Fact]
    public void Translate_IsNullComparison_ReturnsIsNotNullFilterString()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Category == null;
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Category IS NULL", result.ToUpperInvariant());
    }

    /// <summary>
    /// Verifies that comparing a property to not-null translates to an IS NOT NULL check.
    /// </summary>
    [Fact]
    public void Translate_IsNotNullComparison_ReturnsIsNotNullFilterString()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => x.Category != null;
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Category IS NOT NULL", result.ToUpperInvariant());
    }

    /// <summary>
    /// Verifies that compound Not negation on a binary expression (e.g. !(x.Price > 100))
    /// translates to NOT(Price > 100) — exercises the VisitNot → VisitExpression → VisitBinary path.
    /// </summary>
    [Fact]
    public void Translate_CompoundNot_OnBinaryExpression_ReturnsNotWrappedFilterString()
    {
        Expression<Func<FilterTestRecord, bool>> filter = x => !(x.Price > 100);
        var result = ZVecFilterExpressionVisitor.Translate(filter);

        Assert.NotNull(result);
        Assert.Contains("Price > 100", result);
        Assert.Contains("NOT", result.ToUpperInvariant());
    }
```

**Verification**:
1. All new tests pass.
2. The class header comment now correctly reads "all 12 filter operators" (update if it currently says "10").

---

## P-14 — `tests/ZVec.Extensions.VectorData.Tests/ZVecScoreNormalizationTests.cs` (NEW FILE)

```csharp
using Microsoft.Extensions.VectorData;
using ZVec.NET;
using ZVec.NET.Mapping;
using Xunit;

namespace ZVec.Extensions.VectorData.Tests;

/// <summary>
/// TDD unit tests for score normalization in ZVecVectorizableRecordCollection.
/// Verifies the formula matrix documented in docs/architecture/score-semantics.md:
///   Cosine:       similarity = 1.0f - distance
///   L2:           similarity = 1.0f / (1.0f + distance)
///   InnerProduct: similarity = nativeScore (passthrough)
/// </summary>
public sealed class ZVecScoreNormalizationTests
{
    private static string CreateTempStoragePath()
        => Path.Combine(Path.GetTempPath(), "ZVecTests", Guid.NewGuid().ToString("N"));

    private sealed class ScoreTestRecord
    {
        [ZVecId]
        [VectorStoreKey]
        public string Id { get; set; } = string.Empty;

        [ZVecField]
        [VectorStoreData]
        public string Title { get; set; } = string.Empty;

        [ZVecVector(768)]
        [VectorStoreVector(768)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }

    /// <summary>
    /// HIGHER DISTANCE = LOWER SIMILARITY.
    /// Two records with cosine distances 0.1 and 0.5 must produce scores where
    /// the 0.1-distance record has the higher similarity score.
    /// </summary>
    [Fact]
    public async Task SearchAsync_CosineMetric_HigherDistance_ProducesLowerSimilarityScore()
    {
        string storagePath = CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);
        try
        {
            var options = new ZVecVectorStoreOptions { StoragePath = storagePath };
            IZvecFactory factory = new ZVecFactory();
            factory.Initialize();

            string colName = "score_norm_" + Guid.NewGuid().ToString("N")[..8];
            var collection = new ZVecVectorizableRecordCollection<ScoreTestRecord, string>(factory, options, colName);
            await collection.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);

            // Seed two records with very different vectors
            var nearVector = new float[768]; nearVector[0] = 1.0f;
            var farVector = new float[768]; farVector[0] = 0.1f;

            await collection.UpsertAsync(new[]
            {
                new ScoreTestRecord { Id = "near", Title = "near doc", Vector = nearVector },
                new ScoreTestRecord { Id = "far", Title = "far doc", Vector = farVector }
            }, TestContext.Current.CancellationToken);

            // Query with the near vector — "near" record should score higher than "far"
            var queryVector = new float[768]; queryVector[0] = 1.0f;
            var results = new List<VectorSearchResult<ScoreTestRecord>>();
            await foreach (var r in collection.SearchAsync(queryVector, 10, cancellationToken: TestContext.Current.CancellationToken))
            {
                results.Add(r);
            }

            Assert.True(results.Count >= 2, "Search must return at least 2 results to compare scores.");

            var nearResult = results.Find(r => r.Record.Id == "near");
            var farResult = results.Find(r => r.Record.Id == "far");

            Assert.NotNull(nearResult);
            Assert.NotNull(farResult);
            Assert.True(nearResult!.Score > farResult!.Score,
                $"Near record score ({nearResult.Score}) must be greater than far record score ({farResult.Score}). " +
                "If this fails, score normalization formula is wrong — see docs/architecture/score-semantics.md.");

            await collection.EnsureCollectionDeletedAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            if (Directory.Exists(storagePath))
            {
                try { Directory.Delete(storagePath, recursive: true); } catch { }
            }
        }
    }

    /// <summary>
    /// SCORE ORDERING: OrderByDescending(Score) must return best matches first.
    /// This is the core contract for downstream RAG rankers.
    /// </summary>
    [Fact]
    public async Task SearchAsync_Results_AreOrderedByScoreDescending()
    {
        string storagePath = CreateTempStoragePath();
        Directory.CreateDirectory(storagePath);
        try
        {
            var options = new ZVecVectorStoreOptions { StoragePath = storagePath };
            IZvecFactory factory = new ZVecFactory();
            factory.Initialize();

            string colName = "score_order_" + Guid.NewGuid().ToString("N")[..8];
            var collection = new ZVecVectorizableRecordCollection<ScoreTestRecord, string>(factory, options, colName);
            await collection.EnsureCollectionExistsAsync(TestContext.Current.CancellationToken);

            var records = new List<ScoreTestRecord>();
            for (int i = 0; i < 5; i++)
            {
                var v = new float[768];
                v[0] = (5 - i) / 5.0f;  // decreasing similarity to query
                records.Add(new ScoreTestRecord { Id = $"doc{i}", Title = $"doc{i}", Vector = v });
            }
            await collection.UpsertAsync(records, TestContext.Current.CancellationToken);

            var queryVector = new float[768]; queryVector[0] = 1.0f;
            var results = new List<VectorSearchResult<ScoreTestRecord>>();
            await foreach (var r in collection.SearchAsync(queryVector, 5, cancellationToken: TestContext.Current.CancellationToken))
            {
                results.Add(r);
            }

            // Assert descending order
            for (int i = 1; i < results.Count; i++)
            {
                Assert.True(results[i - 1].Score >= results[i].Score,
                    $"Results must be in descending score order. Position {i - 1} score {results[i - 1].Score} < position {i} score {results[i].Score}.");
            }

            await collection.EnsureCollectionDeletedAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            if (Directory.Exists(storagePath))
            {
                try { Directory.Delete(storagePath, recursive: true); } catch { }
            }
        }
    }
}
```

**Verification**:
1. `dotnet test --filter "FullyQualifiedName~ZVecScoreNormalizationTests"` — both tests pass.
2. If tests fail, the score normalization formula in `ZVecVectorizableRecordCollection.NormalizeScore` is wrong — fix the formula, not the test.

---

## P-15 — `tests/ZVec.Extensions.VectorData.ConformanceTests/VectorStoreConformanceFixture.cs`

The current file is NOT a conformance test — it tests reflection on attributes. Two options:

### Option A (preferred): Rename the file to honestly reflect what it tests

1. Rename `VectorStoreConformanceFixture.cs` → `VectorRecordAttributeReflectionTests.cs`
2. Rename the class `VectorStoreConformanceFixture` → `VectorRecordAttributeReflectionTests`
3. Update the test method name from `VectorRecordDefinition_BuildsValidPropertyMetadata` → `VectorRecordClass_DeclaresAllRequiredVectorStoreAttributes`
4. Update the XML doc comment to honestly state: "Tests that the sample POCO is correctly annotated with [VectorStoreKey], [VectorStoreData], [VectorStoreVector] attributes — used for attribute reflection validation, NOT for VectorData contract conformance."

### Option B: Replace with real conformance test

If you have access to the Microsoft.Extensions.VectorData conformance test package (usually shipped as `Microsoft.Extensions.VectorData.ConformanceTests` or similar), reference it and write:

```csharp
public sealed class ZVecVectorStoreConformance : VectorStoreConformanceTests<ZVecVectorStore>
{
    protected override ZVecVectorStore CreateStore(SKContext ctx)
    {
        var options = new ZVecVectorStoreOptions
        {
            StoragePath = Path.Combine(Path.GetTempPath(), "ZVecConformance", Guid.NewGuid().ToString("N"))
        };
        return new ZVecVectorStore(new ZVecFactory(), options);
    }
    // ... implement required abstract hooks
}
```

**If Option B is not possible** (no published conformance package), apply Option A and add a TODO in `project_tasks_implementation_plan.md` Story 1.7: "Block: M.E.VectorData conformance test package is not yet published by Microsoft — track microsoft/semantic-kernel#13224 for upstream release. Currently substituted with `VectorRecordAttributeReflectionTests`."

**Verification**:
1. Build succeeds.
2. Test class name honestly reflects what it tests — no "Conformance" in name unless it actually conforms to a published contract suite.

---

## P-16 — `README.md`

Add Phase 2 status banners to every mention of unimplemented Phase 2 types. Find each occurrence and add the banner immediately above the section heading or bullet point.

### Banner template

```markdown
> **Status:** Planned for Phase 2 (Story 2.x — <story title>)
```

### Specific insertions

| Location (line/section) | Type | Banner to insert above |
|---|---|---|
| Key Features bullet list, "Streaming Citations" | `RagChunk` / `Citation` | `> **Status:** Planned for Phase 2 (Story 2.3 — Citation Tracking & SSE)` |
| Key Features bullet list, "Transparent Document Ingestion" | `IDocumentReader` / `ITextChunker` | `> **Status:** Planned for Phase 2 (Story 2.2 — Document Ingestion)` |
| Key Features bullet list, "Embedded Hybrid Search" | `ZVecRrfReranker` | `> **Status:** Planned for Phase 2 (Story 2.3 — Hybrid Search Bridge)` |
| Quickstart code block | `AddZVecRag`, `HybridSearchOptions`, `IRagIngestor`, `IRagGenerator`, `MapRagSseEndpoint` | Insert above the code block: `> **Status:** Planned for Phase 2 (Stories 2.1, 2.2, 2.3 — RAG Pipeline, Ingestion, SSE)` |
| Document Ingestion Architecture diagram | `IDocumentReader`, `ITextChunker`, `IEmbeddingGenerator` | Insert above diagram: `> **Status:** Planned for Phase 2 (Story 2.2 — Document Ingestion)` |
| Tokenizer Strategy section | `Microsoft.ML.Tokenizers` integration | Insert above: `> **Status:** Planned for Phase 2 (Story 2.2 — Document Ingestion, Task 2.2.4)` |
| Ecosystem Architecture diagram | `IRagIngestor`, `IRagRetriever`, `IRagGenerator`, `IRagPipeline`, `Citation tracking`, `Security Sanitizer`, `MapRagSseEndpoint`, `Token Budget Manager` | Insert above diagram: `> **Status:** Planned for Phase 2 (Stories 2.1, 2.2, 2.3, 2.6)` |

**Verification**: grep `README.md` for `Status:** Planned for Phase 2` — must return at least 6 matches covering every Phase 2 type.

---

## P-17 — `docs/architecture/rag-pipeline.md`

Insert Phase 2 status banners above every mention of unimplemented types:

1. Above the architecture diagram (line 5): `> **Status:** Planned for Phase 2 (Stories 2.1, 2.2, 2.3 — RAG Pipeline, Ingestion, SSE)`
2. Above the "Anti-Corruption Layer" bullet mentioning `IZVecTextChunker`: `> **Status:** Planned for Phase 2 (Story 2.2 — Document Ingestion)`
3. Above the "Embedder Stamp Manifest" bullet mentioning `ZVecIndexManifestManager`: `> **Status:** Planned for Phase 2 (Story 1.11 — Embedder Stamp Manifest)`
4. Above the "Optimize() Lifecycle" bullet mentioning `ReaderWriterLockSlim`: `> **Status:** Planned for Phase 2 (Story 2.3 — Optimize Lifecycle)`
5. Above the "Security Threat Model" bullet mentioning `IRagSecuritySanitizer`: `> **Status:** Planned for Phase 2 (Story 2.6 — Threat Model & Security)`
6. Above the "Context Window Token Budgeting" bullet: `> **Status:** Planned for Phase 2 (Story 2.1 — IRagPipeline, Task 2.1.3)`
7. Above the "Hybrid Search" bullet mentioning `ZVecRrfReranker`: `> **Status:** Planned for Phase 2 (Story 2.3 — Hybrid Search Bridge)`
8. Above the "Citation Tracking" bullet: `> **Status:** Planned for Phase 2 (Story 2.3 — Citation Tracking)`
9. Above the "SSE Response Helpers" bullet mentioning `app.MapRagSseEndpoint`: `> **Status:** Planned for Phase 2 (Story 2.3 — SSE Streaming)`

**Verification**: grep returns at least 9 banner matches in this file.

---

## P-18 — `docs/architecture/security-threat-model.md`

Insert at the very top of the file (after the H1 title):

```markdown
> **Status:** Planned for Phase 2 (Story 2.6 — Threat Model & Security Prompt Injection Filter).
> The `IRagSecuritySanitizer` interface and `DefaultRagSecuritySanitizer` implementation
> described in this document are not yet implemented. This document specifies the design.
```

**Verification**: file begins with the status banner.

---

## P-19 — `docs/architecture/hybrid-search-rrf.md`

Insert at the top (after H1):

```markdown
> **Status:** Planned for Phase 2 (Story 2.3 — Hybrid Search Bridge & RRF).
> The `ZVecRrfReranker` recipe and `HybridSearchOptions.RrfK` configuration
> described in this document are not yet wired through the RAG pipeline.
```

**Verification**: banner present.

---

## P-20 — `docs/architecture/interface-segregation.md`

Insert at the top (after H1):

```markdown
> **Status:** Planned for Phase 2 (Story 2.1 — IRagIngestor, IRagRetriever, IRagGenerator Split Interfaces & RagPipeline Facade).
> The interface segregation design described here is the target architecture for Phase 2.
```

**Verification**: banner present.

---

## P-21 — `docs/architecture/score-semantics.md`

Verify the documented formula matrix matches the implementation in `ZVecVectorizableRecordCollection.NormalizeScore` (P-03). The current documentation reads:

```csharp
float normalizedScore = metricType switch
{
    ZVecMetricType.Cosine => 1.0f - zvecDistance,
    ZVecMetricType.L2 => 1.0f / (1.0f + zvecDistance),
    ZVecMetricType.InnerProduct => zvecRawScore,
    _ => 1.0f - zvecDistance
};
```

If implementation in P-03 differs from this matrix, **update the docs to match the implementation OR fix the implementation to match the docs** — they must be byte-for-byte identical.

**Verification**:
1. `NormalizeScore` in `ZVecVectorizableRecordCollection.cs` matches the matrix in `score-semantics.md` exactly.
2. The math example in `score-semantics.md` (cosine distance 0.2 → similarity 0.8) is correct.

---

## Final Verification Block (run after all patches applied)

### Step 1: Clean build with zero warnings

```bash
dotnet build ZVec.NET-RAG.slnx -c Release
```

**Expected**: `Build succeeded. 0 Warning(s) 0 Error(s).`

### Step 2: Run all tests

```bash
dotnet test ZVec.NET-RAG.slnx -c Release
```

**Expected**: All tests pass. No skipped tests, no `Assert.True(true)`, no `Assert.Empty(results)` documenting stub states.

### Step 3: AOT publish smoke test (local)

```bash
# Windows
dotnet publish tests/ZVec.AotTestApp/ZVec.AotTestApp.csproj -c Release -r win-x64
# Run the published binary — must exit code 0, all 7 tests print success

# WSL2
dotnet publish tests/ZVec.AotTestApp/ZVec.AotTestApp.csproj -c Release -r linux-x64
# Run the published binary — must exit code 0
```

**Expected**: 0 IL2026 warnings, 0 IL3050 warnings, exit code 0, all 7 AOT tests pass.

### Step 4: Verify no test pollution

After running tests, check that no `test_roundtrip_*`, `lifecycle_*`, `listed_*`, `score_*` directories exist under `bin/`:

```bash
find . -path '*/bin/*' -name 'test_roundtrip_*' -o -name 'lifecycle_*' -o -name 'listed_*' -o -name 'score_*'
```

**Expected**: empty output. All test directories must live under `Path.GetTempPath()/ZVecTests/` and be cleaned up by `finally` blocks.

### Step 5: Grep for forbidden patterns

```bash
# Must return ZERO matches:
grep -rn "Assert.True(true)" tests/
grep -rn "Assert.Empty" tests/  # except in tests that legitimately assert empty results (e.g. empty-key rejection)
grep -rn "Assert.NotNull(names)" tests/ZVecVectorStoreTests.cs
grep -rn "for the stub stage" tests/
grep -rn "AppDomain.CurrentDomain.BaseDirectory" src/ZVec.Extensions.VectorData/ZVecVectorizableRecordCollection.cs
grep -rn "AppDomain.CurrentDomain.BaseDirectory" src/ZVec.Extensions.VectorData/ZVecVectorStore.cs
grep -rn "Activator.CreateInstance" src/ZVec.Extensions.VectorData/ZVecVectorizableRecordCollection.cs
# Must return AT LEAST 6 matches:
grep -c "Status:\*\* Planned for Phase 2" README.md
```

### Step 6: Verify SG emits mapper + module initializer

```bash
# Build the connector project, then inspect generated files:
find . -path '*/ZVec.Extensions.VectorData/*' -name '*ZVecMetadataMapper.g.cs' -exec head -5 {} \;
```

**Expected**: each generated file contains:
1. `public static class {ClassName}ZVecMetadataMapper`
2. `public sealed class Mapper : IZVecRecordMapper<{ClassName}>`
3. `[ModuleInitializer]` attribute on `Register()` method

### Step 7: Confirm CI AOT matrix RIDs in plan

Open `project_tasks_implementation_plan.md` and confirm the Verification & Acceptance Matrix documents all 6 RIDs:

```text
win-x64, linux-x64, linux-arm64, osx-arm64, ios-arm64, iossimulator-arm64
```

With RID-aware skips for HNSW-RaBitQ (AVX2-only) and DiskANN (Linux-only).

---

## Acceptance criteria summary

The next review will pass if and only if ALL of the following are true:

1. ✅ `dotnet build ZVec.NET-RAG.slnx -c Release` — 0 warnings
2. ✅ `dotnet test ZVec.NET-RAG.slnx -c Release` — all tests pass
3. ✅ AOT publish for `win-x64` and `linux-x64` — 0 IL2026/IL3050 warnings, exit 0
4. ✅ No `Assert.Empty` documenting stub states anywhere in tests
5. ✅ No `AppDomain.CurrentDomain.BaseDirectory` in production code (only in `ZVecVectorStoreOptions.EffectiveCollectionBasePath` fallback)
6. ✅ No `Activator.CreateInstance` in `MapFromDoc` when SG mapper is registered (only in reflection fallback for `Dictionary<string, object?>`)
7. ✅ `HybridSearchAsync` actually uses `keywords` parameter (per [INSPECT-1] resolution)
8. ✅ `NormalizeScore` switches on `ZVecMetricType` per `score-semantics.md`
9. ✅ SG emits `IZVecRecordMapper<TRecord>` implementation with direct property access
10. ✅ SG emits `[ModuleInitializer]` registration
11. ✅ All Phase 2 type mentions in `README.md` and `docs/architecture/*.md` carry status banners
12. ✅ All 5 new filter visitor tests pass (quote escaping, numeric array, mixed null, IsNull, IsNotNull, compound Not)
13. ✅ `ZVecScoreNormalizationTests` pass (higher distance = lower similarity, descending order)
14. ✅ Test pollution check — no orphan directories under `bin/`
15. ✅ Conformance test file honestly named (Option A) or replaced with real conformance suite (Option B)

---

## What NOT to do

- ❌ Do NOT invent additional tests beyond what's specified here.
- ❌ Do NOT add `#pragma warning disable IL2026` anywhere — fix with `[DynamicallyAccessedMembers]` annotations.
- ❌ Do NOT delete the reflection fallback in `MapFromDoc` — it's required for `Dictionary<string, object?>` dynamic collections.
- ❌ Do NOT change `IZvecCollection` API signatures — only consume them.
- ❌ Do NOT skip the [INSPECT-1/2/3] steps — the patches depend on their answers.
- ❌ Do NOT skip the Final Verification Block — every step must pass.
- ❌ Do NOT add new files beyond those listed in the Patch List table.
- ❌ Do NOT modify `ZVec.NET` reference repo — it is read-only per Rule #10.

---

## End of spec

Apply patches P-01 through P-21 in order, then run the Final Verification Block. The next review will pass.
