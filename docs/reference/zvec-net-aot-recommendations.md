# Native AOT Verification & Trimming Status for `ZVec.NET` (Engine SDK)

> **Specification & Verification Record**
> **Author**: Ahmed Samir (`ahmedsamir50`) | **Org**: Adam Systems
> **Target Repository**: [`ahmedSamir50/AdamSystems.ZVec.NET`](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET)
> **SDK Version**: `1.0.0-beta.5+zvec.0.6.0` | **Status**: Verified 100% Native AOT Clean ✅

---

## Executive Summary

During Phase 0 Native AOT verification of `ZVec.NET` (using `dotnet publish -c Release -r win-x64 /p:PublishAot=true`), an initial audit identified IL trimming warnings (`IL2070` and `IL2091`) in `ZVecTypeModel` and `ZVecMapper` reflection paths.

In `ZVec.NET v1.0.0-beta.5`, these methods were updated with explicit `[DynamicallyAccessedMembers]` annotations. Native AOT execution testing confirmed **100% successful runtime execution** across model resolution, document conversion, vector memory pinning, and POCO restoration.

---

## 1. Applied Code Annotations in `ZVec.NET v1.0.0-beta.5`

### 1.1 `ZVecTypeModel.cs`
Annotated `Get` and `Build` parameters with `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]` to preserve `[ZVecId]`, `[ZVecField]`, and `[ZVecVector]` attribute metadata during ILLink trimming:

```csharp
using System.Diagnostics.CodeAnalysis;

namespace ZVec.NET.Mapping;

public sealed class ZVecTypeModel
{
    public static ZVecTypeModel Get<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >() where T : class => Get(typeof(T));

    public static ZVecTypeModel Get(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        if (!clrType.IsClass || clrType.IsAbstract)
            throw new ArgumentException($"Type '{clrType.Name}' must be a concrete class.", nameof(clrType));

        return Cache.GetOrAdd(clrType, Build);
    }
}
```

### 1.2 `ZVecMapper.cs`
Annotated `ToDoc<T>` and `FromDoc<T>` type parameters:

```csharp
using System.Diagnostics.CodeAnalysis;

namespace ZVec.NET.Mapping;

public static class ZVecMapper
{
    public static ZVecDoc ToDoc<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >(T record) where T : class
    {
        ArgumentNullException.ThrowIfNull(record);
        var model = ZVecTypeModel.Get<T>();
        return ToDoc(record, model);
    }

    public static T FromDoc<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T
    >(ZVecDoc doc) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(doc);
        var model = ZVecTypeModel.Get<T>();
        return FromDoc<T>(doc, model);
    }
}
```

---

## 2. Phase 0 Audit Verification Results

`ZVec.AotTestApp.exe` was compiled with `/p:PublishAot=true` and executed against `ZVec.NET v1.0.0-beta.5`:

```text
=== ZVec.NET Native AOT Audit Starting ===
[AOT Test 1] Generated schema resolved: aot_schema_probe (Vectors: 2, Fields: 0)
[AOT Test 2] ZVecDoc created via generated mapper. Id: doc_aot_001, Fields Count: 2
[AOT Test 3] Document restored: Id=doc_aot_001, Title=AOT Document Test, VectorDim=768
[AOT Test 5] Filter translated: Category = "integration"
[AOT Test 6] Upsert + Get round-trip OK. Fetched Title=AOT Document Test
[AOT Test 7] Vectorized search returned 1 result(s). Top score: 1
=== All Native AOT Verification Tests Passed Successfully ===
```

---

## 3. Impact on `ZVec.Extensions.VectorData` (Phase 1)

Even with `ZVec.NET` annotated, `ZVec.Extensions.VectorData` ships:

1. **`ZVecRecordMetadataGenerator`** (Roslyn Source Generator) — emits static `IZVecRecordMapper<TRecord>` classes, `BuildSchema(collectionName)` factories, and `VectorStoreCollectionDefinition` metadata at build time for `[VectorStore*]` POCOs. Registers via `[ModuleInitializer]` into `ZVecRecordMapperRegistry` and `ZVecCollectionSchemaRegistry`.
2. **`ZVec.Extensions.VectorData.Analyzers`** — emits **`ZVEC001`** / **`ZVEC002`** IDE diagnostics when record types lack generated mappers or reflection is used outside approved fallback paths.
3. **`ZVec.AotTestApp`** — Native AOT audit test app exercising generated schema/mapper round-trip, filter translation via `ZVecFilterRecordModel`, upsert/search, and a **`ReflectionFallbackRecord`** reference to surface trim warnings (`IL2026` / `IL3050`) for non-source-generated types during CI publish.

4. **`ZVec.Rag.AotTestApp`** (Story 2.7, verified) — Pipeline AOT gate for `ZVec.Rag` (M.E.AI + plain-text `IngestTextAsync` via bounded Channels + DI `IZVecTextChunker` + Tiktoken + hybrid retrieve + `AskAsync`). `rag-aot-smoke` fails on `IL2026`/`IL3050`. Excludes `ZVec.Rag.Pdf`, `ZVec.Rag.LLamaSharp`, and SSE.

Filter translation for VectorStore-only POCOs uses `ZVecFilterRecordModel`, which reads source-generated `VectorStoreCollectionDefinition` metadata when `[ZVec*]` attributes are absent.

### CI Quality Gate

[`.github/workflows/quality-gate.yml`](https://github.com/ahmedsamir50/AdamSystems.ZVec.NET-RAG/blob/main/.github/workflows/quality-gate.yml) enforces:

- `dotnet build` + `dotnet format --verify-no-changes`
- Unit and conformance test executables (xUnit v3)
- 500-line class limit and dummy-test detection
- AOT publish + run smoke on `linux-x64`, `win-x64`, `osx-x64`
- Trim-warning verification in publish logs for reflection fallback types

Local pre-commit hook: configure with `git config core.hooksPath .githooks` (see `.githooks/pre-commit`).
