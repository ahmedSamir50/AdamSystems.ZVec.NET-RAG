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
3. **Mock-Free CI Execution**: Core RAG pipeline tests use `DeterministicEmbedder` and `FakeChatClient` in `ZVec.Rag.Testing` to execute in <100ms without downloading multi-GB LLMs.
4. **Snapshot Response Testing**: `Verify.Xunit` snapshot tests validate citation formats and prompt construction.
