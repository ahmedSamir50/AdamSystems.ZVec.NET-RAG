# TDD & Testing Strategy

`ZVec.NET-RAG` mandates a strict Test-Driven Development (TDD) workflow across all components.

> [!NOTE]
> **Implementation Status Banner — Story 2.6 Complete**:
> Zero Dummy Test Enforcement and CI-grade isolated temp directory rules are locked across all test suites.

---

## 1. Zero Dummy / Fake Test Policy

1. **No Dummy Assertions**: `Assert.True(true)`, empty stubs, or 2-line shortcuts are strictly forbidden.
2. **Honest Path Assertions**: Every unit and integration test MUST assert real state changes, CRUD persistence, and parameter validation.
3. **Cancellation Coverage**: Every method accepting `CancellationToken` must include a dedicated test passing a pre-canceled token to verify cancellation guard paths.

---

## 2. CI-Grade Storage Isolation Pattern

All vector collection integration tests that touch disk I/O MUST use isolated, unique temporary storage directories:

```csharp
string tempDir = Path.Combine(Path.GetTempPath(), "ZVecTests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDir);
try
{
    // Execute real ZVec collection CRUD / Search operations
}
finally
{
    if (Directory.Exists(tempDir))
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { }
    }
}
```

---

## 3. Core Testing Rules

1. **Red $\rightarrow$ Green $\rightarrow$ Refactor**: Unit tests must be written BEFORE writing implementation code.
2. **100% Branch Coverage**: All execution paths, edge cases, null checks, and error conditions must be covered.
3. **Mock-Free CI Execution**: Core RAG pipeline tests use `DeterministicEmbedder`, `FakeChatClient`, and `IRagEvaluator` (`DeterministicEvaluator`) in `ZVec.Rag.Testing` to execute in <100ms without downloading multi-GB LLMs.
4. **Snapshot Response Testing**: `Verify.Xunit` snapshot tests validate citation formats and prompt construction.

---

## 4. Test Suites & Conformance Coverage

| Suite | Project | Coverage |
|---|---|---|
| **Unit tests** | `tests/ZVec.Extensions.VectorData.Tests` | Filter visitor operators, error codes, CRUD, hybrid search, score normalization, optimize/reopen recovery |
| **Conformance tests** | `tests/ZVec.Extensions.VectorData.ConformanceTests` | M.E.VectorData contract: lifecycle, CRUD, search, hybrid FTS, zero-vector search, high-dimension vectors, concurrent read/write stress |
| **AOT smoke (connector)** | `tests/ZVec.AotTestApp` | Native AOT publish verification for `ZVec.Extensions.VectorData` (Phase 0 complete) |
| **AOT smoke (pipeline)** | `tests/ZVec.Rag.AotTestApp` | Story 2.7 — full `ZVec.Rag` pipeline AOT gate (M.E.AI + Tiktoken tokenization + text ingest) |

xUnit v3 test projects use executable test assemblies. Run locally:

```bash
dotnet build ZVec.NET-RAG.slnx -c Release
./tests/ZVec.Extensions.VectorData.Tests/bin/Release/net8.0/ZVec.Extensions.VectorData.Tests
./tests/ZVec.Extensions.VectorData.ConformanceTests/bin/Release/net8.0/ZVec.Extensions.VectorData.ConformanceTests
```

---

## 5. CI Quality Gate & Pre-Commit Hook

Automated enforcement lives in [`.github/workflows/quality-gate.yml`](https://github.com/ahmedsamir50/AdamSystems.ZVec.NET-RAG/blob/main/.github/workflows/quality-gate.yml):

- Build, format verification, test executables, 500-line class cap, dummy-test scan
- AOT publish smoke matrix (`linux-x64`, `win-x64`, `osx-x64`)
- Trim-warning verification for non-source-generated record types

Enable the matching local hook:

```bash
git config core.hooksPath .githooks
```
