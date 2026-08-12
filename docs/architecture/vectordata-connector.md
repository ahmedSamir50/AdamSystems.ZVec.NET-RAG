# VectorData Connector Architecture (`ZVec.Extensions.VectorData`)

`ZVec.Extensions.VectorData` provides a zero-allocation, Native AOT trim-safe implementation of Microsoft's official `Microsoft.Extensions.VectorData` specification over the embedded native vector DB engine `ZVec.NET`.

---

## 1. Component Map

```text
┌─────────────────────────────────────────────────────────────┐
│                    Application Layer                        │
│          (Microsoft.Extensions.VectorData Consumers)        │
└──────────────────────────────┬──────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                   ZVecVectorStore                           │
│        Implements IVectorStore backed by IZvecFactory       │
└──────────────────────────────┬──────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│         ZVecVectorizableRecordCollection<TRecord, TKey>     │
│   Implements IVectorStoreRecordCollection<TKey, TRecord>    │
└──────────────┬───────────────────────────────┬──────────────┘
               │                               │
               ▼                               ▼
┌──────────────────────────────┐┌─────────────────────────────┐
│ ZVecFilterExpressionVisitor  ││ ZVecRecordMetadataGenerator │
│ Filter AST Translation Engine││ Roslyn SG Zero-Reflection   │
└──────────────────────────────┘└─────────────────────────────┘
```

---

## 2. Zero-Copy Vector Memory Pinning (`ZVecVectorizableRecordCollection`)

During `VectorizedSearchAsync<TVector>(TVector vector, VectorSearchOptions? options = null)`, input vectors of type `ReadOnlyMemory<float>` are processed using a dual-path zero-copy pin strategy:

```csharp
ReadOnlyMemory<float> floatVector = (ReadOnlyMemory<float>)vector;

if (MemoryMarshal.TryGetArray(floatVector, out ArraySegment<float> segment))
{
    // Fast path: Managed float[] backing array -> Pin directly via fixed statement (0 heap allocations)
    fixed (float* pVector = segment.Array.AsSpan(segment.Offset, segment.Count))
    {
        // Pass pVector directly to native zvec C API P/Invoke
    }
}
else
{
    // Fallback path: MemoryManager<T>-backed memory -> Rent buffer from ArrayPool<float>
    float[] rented = ArrayPool<float>.Shared.Rent(floatVector.Length);
    try
    {
        floatVector.Span.CopyTo(rented);
        fixed (float* pVector = rented)
        {
            // Pass pVector directly to native zvec C API P/Invoke
        }
    }
    finally
    {
        ArrayPool<float>.Shared.Return(rented);
    }
}
```

---

> [!NOTE]
> **Implementation Status Banner — Story 2.1 Complete**:
> `ZVecVectorizableRecordCollection<TRecord, TKey>` is fully wired to native `IZvecCollection` CRUD and search APIs with zero-copy memory pinning and score formula `Score = 1.0f - Distance`.

---

## 3. Central Package Management (CPM)

All NuGet package versions across the solution are managed centrally in `Directory.Packages.props`:

| Package | Purpose | Target Version |
|---|---|---|
| **`ZVec.NET`** | Native Embedded Vector DB Engine | `1.0.0-beta.5` |
| **`Microsoft.Extensions.VectorData.Abstractions`** | Official Vector Store Abstractions | `10.9.0` |
| **`SixLabors.ImageSharp`** | Cross-Platform Image Preprocessing | `3.1.7` |
| **`Microsoft.CodeAnalysis.CSharp`** | Roslyn Source Generator SDK | `4.12.0` |
| **`Microsoft.CodeAnalysis.Analyzers`** | Roslyn Analyzers SDK | `3.11.0` |
| **`xunit.v3`** | Modern Executable Test Platform | `3.2.2` |
| **`xunit.runner.visualstudio`** | Visual Studio & VSTest Test Adapter | `3.1.5` |
| **`Microsoft.NET.Test.Sdk`** | .NET Test SDK Host | `18.8.1` |
| **`coverlet.collector`** | Code Coverage Collector | `10.0.1` |

---

## 4. Core Types & Implementation Files

- **`ZVecVectorStore`**: [`src/ZVec.Extensions.VectorData/ZVecVectorStore.cs`](file:///d:/A_S/ZVec_NET_RAG_SLN/src/ZVec.Extensions.VectorData/ZVecVectorStore.cs)
- **`ZVecVectorStoreOptions`**: [`src/ZVec.Extensions.VectorData/ZVecVectorStoreOptions.cs`](file:///d:/A_S/ZVec_NET_RAG_SLN/src/ZVec.Extensions.VectorData/ZVecVectorStoreOptions.cs) (Storage path routing & factory options).
- **`ZVecVectorizableRecordCollection<TRecord, TKey>`**: [`src/ZVec.Extensions.VectorData/ZVecVectorizableRecordCollection.cs`](file:///d:/A_S/ZVec_NET_RAG_SLN/src/ZVec.Extensions.VectorData/ZVecVectorizableRecordCollection.cs)
- **`ZVecFullTextSearchAttribute`**: [`src/ZVec.Extensions.VectorData/Attributes/ZVecFullTextSearchAttribute.cs`](file:///d:/A_S/ZVec_NET_RAG_SLN/src/ZVec.Extensions.VectorData/Attributes/ZVecFullTextSearchAttribute.cs) (Decorates text properties for native FTS indexing).
- **`IZVecRecordMapper<TRecord>`**: [`src/ZVec.Extensions.VectorData/IZVecRecordMapper.cs`](file:///d:/A_S/ZVec_NET_RAG_SLN/src/ZVec.Extensions.VectorData/IZVecRecordMapper.cs) (Zero-reflection POCO record mapper interface).
- **`ZVecRecordMapperRegistry`**: [`src/ZVec.Extensions.VectorData/ZVecRecordMapperRegistry.cs`](file:///d:/A_S/ZVec_NET_RAG_SLN/src/ZVec.Extensions.VectorData/ZVecRecordMapperRegistry.cs) (Process-wide registry for SG-emitted mappers populated via `[ModuleInitializer]`).
- **`ZVecFilterExpressionVisitor`**: [`src/ZVec.Extensions.VectorData/ZVecFilterExpressionVisitor.cs`](file:///d:/A_S/ZVec_NET_RAG_SLN/src/ZVec.Extensions.VectorData/ZVecFilterExpressionVisitor.cs)
- **`ZVecRecordMetadataGenerator`**: [`src/ZVec.Extensions.VectorData.SourceGenerator/ZVecRecordMetadataGenerator.cs`](file:///d:/A_S/ZVec_NET_RAG_SLN/src/ZVec.Extensions.VectorData.SourceGenerator/ZVecRecordMetadataGenerator.cs) (Emits zero-reflection `IZVecRecordMapper<TRecord>` mappers, `VectorStoreCollectionDefinition`, and `[ModuleInitializer]` registration).
- **`ZVecFilterOperators`**: Enum covering 12 comparison, logical, collection, and null filter operators (`Equals`, `NotEquals`, `LessThan`, `LessThanOrEqual`, `GreaterThan`, `GreaterThanOrEqual`, `And`, `Or`, `Not`, `ContainsAny`, `IsNull`, `IsNotNull`).
- **`ZVecErrorMessages`**: Strongly-typed error formatting helpers eliminating magic strings.
- **`ZVecVectorDataException`**: Base exception type for connector operations.

---

## 5. Filter Expression Translation (`ZVecFilterExpressionVisitor`)

`ZVecFilterExpressionVisitor` translates `Expression<Func<TRecord, bool>>` LINQ predicates into native `ZVecFilterBuilder` AST nodes and SQL-style filter strings.

### Supported Operators (12)

| LINQ Pattern | ZVec AST | Example |
|---|---|---|
| `==` | `Where(Eq)` | `x.Category == "Books"` |
| `!=` | `Where(Ne)` | `x.Category != "Draft"` |
| `<`, `<=`, `>`, `>=` | `Where(Lt/Le/Gt/Ge)` | `x.Price < 100` |
| `&&` | `And` | `x.InStock && x.Price < 50` |
| `\|\|` | `Or` | `x.Category == "A" \|\| x.Category == "B"` |
| `!` | `Not` / bool negation | `!x.InStock` |
| `x.Tags.Contains(value)` | `ContainAny` | `x.Tags.Contains("Sale")` |
| `values.Contains(x.Field)` | `In` | `allowed.Contains(x.Category)` |
| `== null` / `!= null` | `IsNull` / `IsNotNull` | `x.Category == null` |

### Contains Pattern Disambiguation

```text
x.CollectionProperty.Contains(value)  -->  Tags CONTAIN_ANY ("Sale")
externalList.Contains(x.ScalarField)  -->  Category IN ("A", "B")
```

Unsupported string methods (`StartsWith`, `EndsWith`, `Regex.IsMatch`, `string.Contains`) throw `ZVecFilterTranslationException` with explicit remediation guidance pointing to ZVec FTS keyword search.

---

## 6. Score Normalization, Index Optimization & Native AOT Safety

- **Score Normalization Formula:** ZVec native scores are normalized transparently using a metric-switch formula:
  - **Cosine Metric:** \(\text{Score} = 1.0f - d_{\text{cosine}}\) (maps distance \([0, 2]\) to similarity \([-1, 1]\))
  - **L2 Metric:** \(\text{Score} = \frac{1.0f}{1.0f + d_{\text{L2}}}\) (monotonically maps distance \([0, \infty)\) to similarity \((0, 1]\))
  - **InnerProduct Metric:** \(\text{Score} = d_{\text{IP}}\) (passthrough value)
- **Index Optimization & Reopen Lifecycle (`OptimizeAndReopenAsync`):** To prevent stale-querier C++ engine errors post-optimization, `OptimizeAndReopenAsync()` runs native optimization outside the synchronization lock, then performs a short critical section that disposes the previous handle, releases the native `LOCK` file, and reopens a fresh handle. Because ZVec enforces a single read-write handle per collection path, the reopen step must remain inside the lock; the primary concurrency win is that expensive `OptimizeAsync` no longer blocks concurrent readers.
- **FTS Attribute Precedence:** Full-text search indexing is resolved per string property with explicit precedence:
  1. `[ZVecFullTextSearch]` — ZVec-specific source of truth when present (`IsFullTextIndexed` controls enable/disable).
  2. `[VectorStoreData(IsFullTextIndexed = true)]` — fallback for M.E.VectorData consumers when no ZVec FTS attribute is declared.
- **Native AOT & Trim Safety:** All runtime record mapping uses Roslyn Source Generator emitted zero-reflection mappers (`IZVecRecordMapper<TRecord>`). The dynamic reflection fallback is annotated with `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` to ensure Native AOT trim warnings trigger cleanly if an ungenerated record type is used.


