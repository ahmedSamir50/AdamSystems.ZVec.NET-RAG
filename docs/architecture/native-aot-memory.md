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
> - **Story 1.11**: Local dev-loop smoke testing (`win-x64`, `linux-x64`) and GitHub Actions quality gate (`.github/workflows/quality-gate.yml`) run AOT publish smoke on the **3 desktop RIDs** (`linux-x64`, `win-x64`, `osx-x64`) with trim-warning verification for non-source-generated record types. Mobile RIDs (`linux-arm64`, `osx-arm64`, `ios-arm64`, `iossimulator-arm64`, `android-*`) are covered by the upstream `ZVec.NET` package CI, not this repo's connector CI.

---

## Native AOT Verification & RID Matrix

### Local Dev-Loop Smoke vs CI Matrix

| Target RID | Environment | Mode | Details |
|---|---|---|---|
| `win-x64` | Local Dev Machine / CI (`windows-latest`) | Manual Pre-push + CI | Catches ~80% of AOT issues in seconds via `ZVec.AotTestApp.exe`. |
| `linux-x64` | WSL2 / Linux / CI (`ubuntu-latest`) | Manual Pre-push + CI | Local Linux verification loop; primary CI AOT target. |
| `osx-x64` | CI (`macos-latest`) | GitHub Actions | macOS Intel build host (runs under Rosetta on Apple Silicon runners). |
| `linux-arm64` | Upstream `ZVec.NET` CI | Package CI | Validated by the ZVec.NET package's own RID matrix, not re-verified here. |
| `osx-arm64` | Upstream `ZVec.NET` CI | Package CI | Apple Silicon native binary; validated upstream. |
| `ios-arm64` | Upstream `ZVec.NET` CI | Package CI | Flagship iOS target; native binary shipped in the ZVec.NET NuGet. |
| `iossimulator-arm64` | Upstream `ZVec.NET` CI | Package CI | iOS Simulator target; validated upstream. |
| `android-arm64` / `android-x64` | Upstream `ZVec.NET` CI | Package CI | Android native binaries; validated upstream. |

> **Connector CI scope:** This repo's `aot-smoke` job verifies the connector's
> source-generated mappers and filter evaluator under Native AOT on the 3 desktop
> RIDs. The mobile RIDs are exercised by the upstream `ZVec.NET` package CI (which
> owns the native C++ core and its 9-RID build matrix). To expand connector CI to a
> mobile RID, add the runner image and RID to the `aot-smoke` matrix in
> `.github/workflows/quality-gate.yml`.

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

