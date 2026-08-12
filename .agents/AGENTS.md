# ZVec.NET-RAG — Agent Behavioral Rules & Collaboration Guidelines

## Core Philosophy: Local-First, Zero-Cloud, Ecosystem-Native

This project (`ZVec.NET-RAG`) builds `ZVec.Extensions.VectorData` (a `Microsoft.Extensions.VectorData` connector) and `ZVec.Rag` (a batteries-included local-first RAG starter) on top of the native embedded vector DB engine `ZVec.NET`.

Author: **Ahmed Samir** (`ahmedsamir50`) | Org: **Adam Systems**

---

## 1. Uncompromising Pushback & Quality Guidelines

All expert agents working in this workspace MUST adhere to the following non-negotiable principles:

1. **Zero Dummy / Fake / Placeholder Tests**:
   - ABSOLUTELY NO `Assert.True(true)`, empty test stubs, or superficial 2-line shortcuts.
   - Every single test case MUST be an **honest, full test case** asserting real behavior, contract adherence, parameter validation, edge cases, and exception paths.
   - Any test that returns trivial success without exercising real logic is considered a critical quality violation.

2. **"Never Blindly Agree" Rule & Risk Mitigations**:
   - Agents MUST NEVER blindly accept user opinions, design proposals, or peer suggestions.
   - When evaluating any architecture, feature, or code change, agents must actively identify risks, edge cases, performance bottlenecks, and ecosystem gaps.
   - Agents must present solutions, fixes, and mitigations structured clearly with **Options, Pros, and Cons / Drawbacks**.

3. **Strict SOLID Principles & 500-Line Class Limit**:
   - Enforce strict SOLID design principles across all components.
   - Absolutely NO God classes or monolithic orchestrators.
   - **Hard Rule**: No class may exceed 500 lines of code. If a class approaches this limit, it must be decomposed into single-responsibility interfaces and sub-components (unless maintaining tightly bound structural state explicitly requires it).

4. **Test-Driven Development (TDD) Mandatory**:
   - Write failing unit tests before writing implementation code for any public or internal method.
   - Test cases must achieve **maximum execution path coverage**. If a method has multiple branches, **all branches must be tested**.
   - Provide abstraction layers whenever appropriate. Almost all abstraction methods must have **at least one honest full test case**.

5. **Universal XML Documentation & Code Illustrations**:
   - Every public, protected, and internal type, interface, method, property, and field must have complete XML comments (`/// <summary>`, `<param>`, `<returns>`, `<exception>`).
   - Hot paths, complex algorithms, ambiguous code paths, and critical/hard logic MUST include inline code illustrations, ASCII flow diagrams, or detailed explanatory comments to ensure human maintainability and clarity of intent.

6. **No Magic / Hardcoded Strings**:
   - Never use hardcoded string literals for filter operators, configuration keys, collection names, error messages, or internal tokens.
   - Use strongly typed `enum` definitions or `static class` containers (e.g. `ZVecFilterOperators`, `ZVecErrorMessages`, `ZVecConstants`).

7. **Code Reviewer Approval Gate**:
   - No code changes are accepted without explicit review and approval by the `zvec-code-reviewer-expert` agent.

8. **MkDocs Wiki Synchronization**:
   - All architecture, math, dependencies, theory, scripts, and API changes must be documented in `docs/` (`mkdocs.yml`). Documentation must be kept up to date after every single approved change.

9. **ZVec.NET Reference Integrity**:
   - The reference ZVec.NET repository (`ahmedSamir50/AdamSystems.ZVec.NET`) is located at `D:\A_S\ZVec.Net_SLN\ZVec.Net`.
   - Agents are allowed to inspect, search, and verify code signatures in `D:\A_S\ZVec.Net_SLN\ZVec.Net`.
   - **CRITICAL RESTRICTION**: This workspace is **NEVER ALLOWED** to modify or write files in `D:\A_S\ZVec.Net_SLN\ZVec.Net`.

10. **"Integrate, Don't Reimplement"**:
    - Always leverage `Microsoft.Extensions.VectorData`, `Microsoft.Extensions.AI`, and `Microsoft.Extensions.DataIngestion`. Push back forcefully on custom reimplementations.

11. **Performance & Zero Allocation**:
    - Vectors must be passed via `ReadOnlySpan<float>` / `ReadOnlyMemory<float>` pin paths without array copying or heap allocations.

12. **Strict Test Coverage & Honesty Gate**:
    - No task or story may be marked completed (`[x]`) unless a full code review and test honesty audit is conducted.
    - Assertions on stubbed methods returning `yield break;` or empty defaults MUST NOT be accepted as proof of feature completion.
    - Every single execution path, exception guard (`if (null)`), value type constraint check, and AST branch MUST have a dedicated, non-dummy unit test case.

---

## 2. Expert Roles & Responsibilities

- **`zvec-architect-strategy-expert`**: Product positioning, competitive moat, kill criteria monitoring, developer onboarding (`dotnet new rag`), enterprise vs OSS licensing, risk mitigation options with Pros/Cons.
- **`zvec-vectordata-expert`**: Conformance to `Microsoft.Extensions.VectorData`, AST filter translation (`VectorDataFilter` -> `ZVecFilterBuilder`), Roslyn Source Generation, schema mappings.
- **`zvec-rag-pipeline-expert`**: Integration with `M.E.AI` and `M.E.DataIngestion`, hybrid search (dense + FTS + RRF), citation tracking, SSE streaming, MAUI/ASP.NET recipes, test fakes.
- **`zvec-native-aot-expert`**: Native interop with ZVec C++ core, `SafeZvecHandle` lifecycle, zero-copy memory pinning, Native AOT trim auditing, 9 multi-platform RIDs.
- **`zvec-code-reviewer-expert`**: TDD compliance, 100% path coverage auditing, zero magic strings, strict SOLID & <500 lines enforcement, XML doc & code illustration audit, MkDocs wiki sync, **Zero Dummy Test Enforcement**.
- **`zvec-performance-expert`**: Zero-allocation hot paths, BenchmarkDotNet profiling, memory pinning, GC pressure minimization, ArrayPool reuse, cache efficiency.

---

## 3. Code & Design Standards

- **Target Frameworks**: `.NET 8`, `.NET 9`, `.NET 10` (LTS floor `.NET 8`).
- **AOT & Trimming**: All public APIs must be annotated with `[DynamicallyAccessedMembers]` where needed and verified via `PublishAot=true` CI checks.
- **Async Pattern**: All I/O operations must expose `ValueTask` / `Task` signatures accepting `CancellationToken`.
- **Zero-Allocation Hot Paths**: Query vectors must be passed via `ReadOnlySpan<float>` / `ReadOnlyMemory<float>` without array copies.
