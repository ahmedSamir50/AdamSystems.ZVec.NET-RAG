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

> [!NOTE]
> **Implementation Status Banner — Story 1.10 & Story 1.11 Complete**:
> - **Story 1.10**: `ZVecFilterExpressionVisitor` uses an AOT-safe recursive AST evaluator eliminating `Expression.Compile().DynamicInvoke()`.
> - **Story 1.11**: Local dev-loop smoke testing (`win-x64`, `linux-x64`) and GitHub Actions quality gate (`.github/workflows/quality-gate.yml`) run AOT publish smoke on `linux-x64`, `win-x64`, and `osx-x64` with trim-warning verification for non-source-generated record types.

---

## Native AOT Verification & RID Matrix

### Local Dev-Loop Smoke vs CI Matrix

| Target RID | Environment | Mode | Details |
|---|---|---|---|
| `win-x64` | Local Dev Machine | Manual Pre-push | Catches ~80% of AOT issues in seconds via `ZVec.AotTestApp.exe`. |
| `linux-x64` | WSL2 / Linux | Manual Pre-push | Local Linux verification loop. |
| `linux-arm64` | CI (`ubuntu-24.04-arm`) | GitHub Actions | Skips HNSW-RaBitQ (x86_64 AVX2 only). |
| `osx-arm64` | CI (`macos-14`) | GitHub Actions | Apple Silicon macOS build host. |
| `ios-arm64` | CI (`macos-14`) | GitHub Actions | Flagship iOS target (`maui-ios` workload). |
| `iossimulator-arm64` | CI (`macos-14`) | GitHub Actions | iOS Simulator target. |

### AOT Filter Evaluator Mechanics

Under Native AOT, dynamic IL generation via `Expression.Compile().DynamicInvoke()` is prohibited as it causes runtime `IL3050`/`IL2026` crashes. `ZVecFilterExpressionVisitor` evaluates expressions statically without dynamic compilation:
- **`ConstantExpression`**: Evaluates literal values directly.
- **`MemberExpression`**: Extracts field/property values from closure objects or static classes.
- **`NewArrayExpression`**: Constructs array instances statically.
- **`op_Implicit` / `op_Explicit`**: Unwraps **approved BCL** conversion operators (`decimal`, numeric primitives) and `ReadOnlySpan` array bridges only. User-defined conversion operators throw `ZVecFilterTranslationException` with `UnsupportedUserDefinedConversion`.
- **`MethodCallExpression`**: Evaluates static method calls and helper functions safely under AOT.

---

## Roslyn AOT Analyzers (`ZVec.Extensions.VectorData.Analyzers`)

Compile-time diagnostics supplement publish-time trim warnings:

| ID | Severity (default) | Description |
|---|---|---|
| **`ZVEC001`** | Warning | `[VectorStoreRecord]` / mapping-decorated type lacks source-generated `IZVecRecordMapper<T>` registration |
| **`ZVEC002`** | Warning | Reflection API (`Type.GetProperty`, `Activator.CreateInstance`, etc.) used outside `[RequiresUnreferencedCode]` fallback paths |

Configure severity in [`.editorconfig`](https://github.com/ahmedsamir50/AdamSystems.ZVec.NET-RAG/blob/main/.editorconfig). CI enforces analyzers, formatting, tests, and AOT publish via [`.github/workflows/quality-gate.yml`](https://github.com/ahmedsamir50/AdamSystems.ZVec.NET-RAG/blob/main/.github/workflows/quality-gate.yml).

