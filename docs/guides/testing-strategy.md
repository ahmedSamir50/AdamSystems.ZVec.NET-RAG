# TDD & Testing Strategy

`ZVec.NET-RAG` mandates a strict Test-Driven Development (TDD) workflow across all components.

> [!NOTE]
> **Implementation Status Banner — Stories 2.4–2.6 (shipped)**:
> Zero Dummy Test Enforcement and CI-grade isolated temp directory rules are locked across all test suites.
> `Verify.XunitV3` snapshots (2.4.3), package READMEs (2.5), and `IRagSecuritySanitizer` (2.6) are shipped.
> `ZVec.Rag.AotTestApp` harness exists; Story 2.7 remains unchecked until Task 2.7.3 passes.
> `IRagEvaluator` / `DeterministicEvaluator` ship in Story 2.8 — not core CI today.

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
3. **Mock-Free CI Execution**: Core RAG pipeline tests use `DeterministicEmbedder` and `FakeChatClient` in `ZVec.Rag.Testing`. `IRagEvaluator` / `DeterministicEvaluator` arrive in Story 2.8.
4. **Snapshot Response Testing**: `Verify.XunitV3` snapshot tests (Story 2.4.3) validate citation formats and prompt construction.

---

## 4. Test Suites & Conformance Coverage

| Suite | Project | Coverage |
|---|---|---|
| **Unit tests** | `tests/ZVec.Extensions.VectorData.Tests` | Filter visitor operators, error codes, CRUD, hybrid search, score normalization, optimize/reopen recovery |
| **Conformance tests** | `tests/ZVec.Extensions.VectorData.ConformanceTests` | M.E.VectorData contract: lifecycle, CRUD, search, hybrid FTS, zero-vector search, high-dimension vectors, concurrent read/write stress |
| **RAG pipeline** | `tests/ZVec.Rag.Tests` | IRag*, ContextPacker, `DeterministicEmbedder`, `FakeChatClient`, real ZVec round-trip |
| **AOT smoke (connector)** | `tests/ZVec.AotTestApp` | Native AOT publish verification for `ZVec.Extensions.VectorData` (Phase 0 complete) |
| **AOT smoke (pipeline)** | `tests/ZVec.Rag.AotTestApp` | Harness ships; Story 2.7 gate closes when `rag-aot-smoke` passes Task 2.7.3 on 3 desktop RIDs |

xUnit v3 test projects use executable test assemblies. Run locally:

```bash
dotnet build ZVec.NET-RAG.slnx -c Release
./tests/ZVec.Extensions.VectorData.Tests/bin/Release/net8.0/ZVec.Extensions.VectorData.Tests
./tests/ZVec.Extensions.VectorData.ConformanceTests/bin/Release/net8.0/ZVec.Extensions.VectorData.ConformanceTests
./tests/ZVec.Rag.Tests/bin/Release/net8.0/ZVec.Rag.Tests
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
