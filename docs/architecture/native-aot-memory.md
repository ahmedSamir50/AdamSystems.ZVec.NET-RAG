# Native AOT & Zero-Copy Memory Pinning

To guarantee maximum query performance and Native AOT trim safety, `ZVec.NET` and `ZVec.Extensions.VectorData` enforce zero-copy memory pinning.

## P/Invoke Pinning Hot Path

Vectors represented as `ReadOnlyMemory<float>` are pinned directly during native C++ interop:

```csharp
using var handle = memory.Pin();
unsafe
{
    float* ptr = (float*)handle.Pointer;
    // Direct P/Invoke call passing ptr to native zvec C API
}
```

This prevents managed heap array allocations (`float[]`) and ensures zero GC pressure during vector search operations.

---

## Native AOT Verification Status

As of `ZVec.NET v1.0.0-beta.5`, native AOT compilation is verified clean via the Phase 0 audit harness ([`ZVec.AotTestApp`](file:///d:/A_S/ZVec_NET_RAG_SLN/tests/ZVec.AotTestApp/Program.cs)).

`ZVec.Extensions.VectorData` will further enhance Native AOT performance via `ZVecRecordMetadataGenerator` (Roslyn Source Generator), emitting compile-time mappers for `[VectorStoreRecord]` POCOs with zero runtime reflection.
