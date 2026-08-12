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

## 3. Central Package Management (CPM)

All NuGet package versions across the solution are managed centrally in `Directory.Packages.props`:

| Package | Purpose | Target Version |
|---|---|---|
| **`ZVec.NET`** | Native Embedded Vector DB Engine | `1.0.0-beta.5` |
| **`Microsoft.Extensions.VectorData.Abstractions`** | Official Vector Store Abstractions | `10.8.2` |
| **`SixLabors.ImageSharp`** | Cross-Platform Image Preprocessing | `3.1.7` |
| **`Microsoft.CodeAnalysis.CSharp`** | Roslyn Source Generator SDK | `5.6.0` |
| **`Microsoft.CodeAnalysis.Analyzers`** | Roslyn Analyzers SDK | `5.6.0` |
| **`xunit.v3`** | Modern Executable Test Platform | `3.2.2` |
| **`xunit.runner.visualstudio`** | Visual Studio & VSTest Test Adapter | `3.1.5` |
| **`Microsoft.NET.Test.Sdk`** | .NET Test SDK Host | `18.8.1` |
| **`coverlet.collector`** | Code Coverage Collector | `10.0.1` |

---

## 4. Core Types & Implementation Files

- **`ZVecVectorStore`**: [`src/ZVec.Extensions.VectorData/ZVecVectorStore.cs`](file:///d:/A_S/ZVec_NET_RAG_SLN/src/ZVec.Extensions.VectorData/ZVecVectorStore.cs)
- **`ZVecVectorizableRecordCollection<TRecord, TKey>`**: [`src/ZVec.Extensions.VectorData/ZVecVectorizableRecordCollection.cs`](file:///d:/A_S/ZVec_NET_RAG_SLN/src/ZVec.Extensions.VectorData/ZVecVectorizableRecordCollection.cs)
- **`ZVecFilterExpressionVisitor`**: [`src/ZVec.Extensions.VectorData/ZVecFilterExpressionVisitor.cs`](file:///d:/A_S/ZVec_NET_RAG_SLN/src/ZVec.Extensions.VectorData/ZVecFilterExpressionVisitor.cs)
- **`ZVecRecordMetadataGenerator`**: [`src/ZVec.Extensions.VectorData.SourceGenerator/ZVecRecordMetadataGenerator.cs`](file:///d:/A_S/ZVec_NET_RAG_SLN/src/ZVec.Extensions.VectorData.SourceGenerator/ZVecRecordMetadataGenerator.cs) (Emits zero-reflection record mappers and static schema builders calling `AddField()` / `AddVector()` directly).
- **`ZVecFilterOperators`**: Enum covering 12 comparison, logical, collection, and null filter operators (`Equals`, `NotEquals`, `LessThan`, `LessThanOrEqual`, `GreaterThan`, `GreaterThanOrEqual`, `And`, `Or`, `Not`, `ContainsAny`, `IsNull`, `IsNotNull`).
- **`ZVecErrorMessages`**: Strongly-typed error formatting helpers eliminating magic strings.
- **`ZVecVectorDataException`**: Base exception type for connector operations.

---

## 5. Score Normalization & Filter Translation Boundaries

- **Score Normalization:** ZVec native Cosine distance is normalized transparently via `Score = 1.0f - ZVecDistance` so `VectorSearchResults<TRecord>.Score` returns normalized similarity (higher = better). See [Score Semantics Architecture](score-semantics.md).
- **Filter Translation Boundaries:** `ZVecFilterExpressionVisitor` translates LINQ expressions into `ZVecFilterBuilder` AST nodes. It inspects `MethodCallExpression` for `Enumerable.Contains` / `List<T>.Contains` on tag/array properties and translates them to `ZVecFilterBuilder.ContainAny`. Method calls unsupported by ZVec engine (e.g. `StartsWith`, `EndsWith`, `Regex.IsMatch`) throw a strongly-typed `ZVecFilterTranslationException`.

