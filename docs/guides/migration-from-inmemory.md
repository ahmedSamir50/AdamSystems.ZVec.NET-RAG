# Migrating from `Microsoft.Extensions.VectorData.InMemory` to `ZVec.Extensions.VectorData`

`Microsoft.Extensions.VectorData.InMemory` is a testing-only vector store (per
[Microsoft's official docs](https://learn.microsoft.com/dotnet/api/microsoft.extensions.vectordata.inmemory)).
It keeps all data in process memory and **loses everything on restart**. `ZVec.Extensions.VectorData`
backs the same `Microsoft.Extensions.VectorData` abstractions with the embedded, persistent
native vector DB engine `ZVec.NET` — single-file on disk, no server, no cloud.

This guide walks through migrating an existing app from the InMemory connector to ZVec.

---

## 1. Why migrate?

| Concern | `VectorData.InMemory` | `ZVec.Extensions.VectorData` |
|---------|------------------------|------------------------------|
| Persistence | None — data lost on restart | Single-file on disk (`*.zvec`) |
| Production use | Explicitly **not for production** per Microsoft docs | Production-grade (Apache-2.0, 9 native RIDs) |
| Hybrid search | Not supported | Native dense + FTS + RRF reranker |
| Indexes | Flat scan only | HNSW, Flat, IVF, Vamana, DiskANN, FTS |
| AOT / trim | N/A | Source-generated mappers, AOT smoke CI |
| Footprint | In-process | In-process (native C++ core, 139 MB NuGet) |
| RIDs | All managed | win-x64, linux-x64/arm64, osx-x64/arm64, android-*, ios-* |

If you are using `VectorData.InMemory` for **unit tests**, keep it. If you need
**persistence, hybrid search, or production deployment**, migrate to ZVec.

---

## 2. Package swap

Replace the InMemory package reference with the ZVec connector:

```xml
<!-- Before -->
<PackageReference Include="Microsoft.Extensions.VectorData.InMemory" Version="9.0.0-preview.1.25078.1" />

<!-- After -->
<PackageReference Include="ZVec.Extensions.VectorData" Version="0.1.0" />
<PackageReference Include="ZVec.NET" Version="1.0.0-beta.6" />
```

The ZVec connector depends on `Microsoft.Extensions.VectorData.Abstractions` (the same
abstractions you already target), so your application code that consumes
`VectorStore` / `IVectorizedSearch<TRecord>` / `IKeywordHybridSearchable<TRecord>` does
not change.

---

## 3. DI registration swap

```csharp
// Before
using Microsoft.Extensions.VectorData.InMemory;
builder.Services.AddInMemoryVectorStore();

// After
using ZVec.Extensions.VectorData;
builder.Services.AddZVecVectorStore(opts =>
{
    opts.StoragePath = "./data";          // directory holding *.zvec collection files
    opts.MaxConcurrentNativeCalls = Environment.ProcessorCount;
});
```

`AddZVecVectorStore` registers both the concrete `ZVecVectorStore` and the abstract
`VectorStore` base class, so any consumer injecting `VectorStore` keeps working
unchanged.

---

## 4. Record definitions: dual annotation

The InMemory connector reads only `[VectorStoreKey]`, `[VectorStoreData]`, and
`[VectorStoreVector]`. The ZVec connector's source generator reads those same
attributes for mapper emission, but the underlying native engine also needs ZVec
schema attributes for index/dimension metadata. Annotate each record with **both**
families:

```csharp
using Microsoft.Extensions.VectorData;
using ZVec.NET.Mapping;

public class RagDocumentChunk
{
    [VectorStoreKey]
    [ZVecId]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    [ZVecFullTextSearch]                       // ZVec FTS marker (takes precedence)
    public string Content { get; set; } = string.Empty;

    [VectorStoreData]
    [ZVecField]
    public string Source { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions = 768)]
    [ZVecVector(Dimension = 768, Index = ZVecIndexType.Hnsw)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
```

| M.E.VectorData attribute | ZVec equivalent | Purpose |
|--------------------------|-----------------|---------|
| `[VectorStoreKey]` | `[ZVecId]` | Identity property |
| `[VectorStoreData]` | `[ZVecField]` | Scalar field |
| `[VectorStoreData(IsFullTextIndexed = true)]` | `[ZVecFullTextSearch]` | FTS-indexed text |
| `[VectorStoreVector(Dimensions = N)]` | `[ZVecVector(Dimension = N, Index = ...)]` | Dense vector + index kind |

> The source generator (`ZVecRecordMetadataGenerator`) emits a zero-reflection
> `IZVecRecordMapper<TRecord>` for every type decorated with the `VectorStore*`
> attributes, so dual annotation does not cost you AOT cleanliness.

---

## 5. Collection lifecycle

The InMemory connector creates collections in memory implicitly. ZVec persists
collections as files on disk:

```csharp
var store = serviceProvider.GetRequiredService<VectorStore>();
var collection = store.GetCollection<string, RagDocumentChunk>("chunks");

// Create the collection file on disk (idempotent).
await collection.EnsureCollectionExistsAsync();

// Upsert / search / fetch as before.
await collection.UpsertAsync(record);
await foreach (var hit in collection.SearchAsync(queryVector, top: 5))
    Console.WriteLine(hit.Score);
```

Collections survive process restarts. To delete a collection and its files:

```csharp
await collection.EnsureCollectionDeletedAsync();
```

---

## 6. Hybrid search (new capability)

The InMemory connector has no hybrid search. ZVec exposes it via the same
`IKeywordHybridSearchable<TRecord>` interface from `Microsoft.Extensions.VectorData`:

```csharp
if (collection is IKeywordHybridSearchable<RagDocumentChunk> hybrid)
{
    var results = hybrid.HybridSearchAsync(
        searchValue: queryVector,
        keywords: new[] { "refund", "policy" },
        top: 5,
        options: new ZVecHybridSearchOptions<RagDocumentChunk>
        {
            RrfK = 60,                       // RRF smoothing constant (default 60)
            ScoreThreshold = 0.1f,
        });

    await foreach (var hit in results)
        Console.WriteLine($"{hit.Score:0.000}  {hit.Record.Content}");
}
```

`ZVecHybridSearchOptions<TRecord>` derives from `HybridSearchOptions<TRecord>` and
adds the ZVec-native `RrfK` knob. Use the base `HybridSearchOptions<TRecord>` if you
do not need to tune RRF.

---

## 7. Score semantics

The InMemory connector returns raw similarity scores. ZVec returns native distance
scores, which the connector normalizes to similarity based on the configured metric:

| Metric | Normalized score |
|--------|------------------|
| Cosine | `1.0 - distance` (range `[-1, 1]`) |
| L2     | `1.0 / (1.0 + distance)` (range `(0, 1]`) |
| InnerProduct | passthrough |

Existing score-threshold filters should be revalidated after migration, because the
numeric range may differ from the InMemory connector's.

---

## 8. AOT / trim considerations

The InMemory connector is fully managed and has no AOT constraints. ZVec's native
core is AOT-friendly, but record mapping has two paths:

- **Source-generated mapper** (default for annotated POCOs) — zero reflection, AOT-clean.
- **Reflection fallback** (only for `Dictionary<string, object?>` dynamic collections
  or un-annotated records) — annotated with `[RequiresUnreferencedCode]` and
  `[RequiresDynamicCode]`; surfaces `IL2026` / `IL3050` trim warnings.

To stay AOT-clean:

1. Decorate every record type with `VectorStore*` attributes (the source generator
   picks them up automatically).
2. Watch for `ZVEC001` (missing source-generated mapper) and `ZVEC002` (reflection
   in a non-fallback path) analyzer warnings in your build.
3. Run the `ZVec.AotTestApp` smoke test or the CI `aot-smoke` job as a final gate.

---

## 9. Migration checklist

- [ ] Replace `Microsoft.Extensions.VectorData.InMemory` with `ZVec.Extensions.VectorData` + `ZVec.NET`.
- [ ] Swap `AddInMemoryVectorStore()` for `AddZVecVectorStore(opts => ...)`.
- [ ] Add ZVec attributes (`[ZVecId]`, `[ZVecField]`, `[ZVecVector]`, `[ZVecFullTextSearch]`) alongside the existing `VectorStore*` attributes.
- [ ] Call `EnsureCollectionExistsAsync()` on each collection before first use.
- [ ] Revalidate score thresholds against the normalized score ranges.
- [ ] Opt into hybrid search via `IKeywordHybridSearchable<TRecord>` if you need FTS.
- [ ] Build with `TreatWarningsAsErrors=true` and resolve any `ZVEC001` / `ZVEC002` warnings.
- [ ] Run the `aot-smoke` CI job (or `dotnet publish -r linux-x64 /p:PublishAot=true` locally) before shipping.

---

## 10. Fallback to InMemory for tests

You can keep `VectorData.InMemory` as a test-only dependency and register ZVec in
production via environment-driven DI:

```csharp
if (env.IsDevelopment() && useInMemoryForTests)
    builder.Services.AddInMemoryVectorStore();
else
    builder.Services.AddZVecVectorStore(opts => opts.StoragePath = "./data");
```

Both connectors implement the same `VectorStore` abstraction, so test fixtures and
production code share the same application layer.

---

## 11. Changing quantization or embedder (rebuild required)

If you change `DefaultQuantizeType`, vector dimensions, or the embedding model on an
existing ZVec collection, you must **delete the collection and re-ingest** (or use
`IRagMigrationManager` when available). `EnsureSchema` cannot requantize an HNSW index
in place.

The embedder stamp manifest (`zvec_index_manifest.json`) records `ModelId`,
`Dimensions`, and `QuantizeType`. A mismatch throws `ZVecEmbedderMismatchException` —
wrap as `ZVecRagInitializationException` in `ZVec.Rag` with a clear delete-path hint.

See [Quantization & Index Rebuild Guide](quantization.md) for mobile Sample 03 policy
(Flat default; optional INT8 only after Recall@K gate).
