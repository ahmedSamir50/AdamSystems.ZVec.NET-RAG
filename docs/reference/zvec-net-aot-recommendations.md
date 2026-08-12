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
=== ZVec.NET Native AOT Audit Harness Starting ===
[AOT Test 1] Model resolved: SampleAotDoc (Id: Id, Fields: 1, Vectors: 1)
[AOT Test 2] ZVecDoc created successfully. Id: doc_aot_001, Fields Count: 1
[AOT Test 3] Document restored: Id=doc_aot_001, Title=AOT Document Test, VectorDim=768
=== All Native AOT Verification Tests Passed Successfully ===
```

---

## 3. Impact on `ZVec.Extensions.VectorData` (Phase 1)

Even with `ZVec.NET` annotated, `ZVec.Extensions.VectorData` will ship `ZVecRecordMetadataGenerator` (Roslyn Source Generator) in Phase 1 to generate static `IVectorRecordMapper<TRecord>` classes at build time for `[VectorStoreRecord]` POCOs. This completely eliminates runtime reflection overhead and guarantees zero GC allocation on query hot paths.
