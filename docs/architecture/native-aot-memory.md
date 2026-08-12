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
> - **Story 1.11**: Local dev-loop smoke testing (`win-x64`, `linux-x64`) and full GitHub Actions CI AOT matrix (`win-x64`, `linux-x64`, `linux-arm64`, `osx-arm64`, `ios-arm64`, `iossimulator-arm64`) run with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.

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
- **`op_Implicit` / `op_Explicit`**: Unwraps implicit/explicit conversion operator calls.
- **`MethodCallExpression`**: Evaluates static method calls and helper functions safely under AOT.

