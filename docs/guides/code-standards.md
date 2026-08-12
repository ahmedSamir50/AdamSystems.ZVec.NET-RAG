# Code Standards & Quality Guidelines

This document outlines the non-negotiable coding standards, design principles, and quality gates for `ZVec.NET-RAG`.

---

## 1. Zero Dummy / Fake / Placeholder Tests

- ABSOLUTELY NO `Assert.True(true)`, empty test stubs, or superficial shortcuts.
- Every test case MUST be an **honest, full test case** asserting real behavior, contract adherence, parameter validation, edge cases, and exception paths.
- Any test that returns trivial success without exercising real logic is considered a critical quality violation.

---

## 2. "Never Blindly Agree" Rule & Risk Mitigations

- All agents and team members MUST NEVER blindly accept design proposals or assumptions.
- When reviewing architectural choices, API contracts, or optimizations, evaluate potential risks, edge cases, and drawbacks.
- Present solutions and mitigations structured with clear **Options, Pros, and Cons / Drawbacks**.

---

## 3. Strict SOLID Principles & 500-Line Class Limit

- **Full SOLID Compliance**:
  - Every component must adhere strictly to SOLID design principles.
- **500-Line Class Cap**:
  - No class may exceed **500 lines of code**. Monolithic classes or orchestrators are strictly prohibited.
  - Decompose large classes into single-responsibility interface abstractions and focused helper components.

---

## 4. Test-Driven Development (TDD) & Test Coverage

- **Red $\rightarrow$ Green $\rightarrow$ Refactor**:
  - Write failing unit tests before writing implementation code for any public or internal method.
- **100% Branch & Execution Path Coverage**:
  - Every public method must have dedicated test cases covering **all execution paths**, edge cases, invalid inputs, and error states.
  - Abstractions (`interfaces`, `abstract classes`) must have at least one honest full test case validating their contract behavior.

---

## 5. Zero Magic Strings / Hardcoded Values

- **Enums & Static Constant Classes**:
  - No hardcoded string literals for filter operators, configuration keys, collection names, error messages, or internal tokens.
- All constants must be strongly typed using `enum` definitions or `static class` containers (e.g., `ZVecFilterOperators`, `ZVecFilterErrorCode`, `ZVecErrorMessages`, `ZVecConstants`).

---

## 6. Universal XML Documentation & Code Illustrations

- **100% XML Doc Coverage**:
  - Every public, protected, and internal type, interface, method, property, and field must include complete XML doc comments (`/// <summary>`, `<param>`, `<returns>`, `<exception>`).
- **Code Illustrations for Humans**:
  - Hot paths, complex algorithms, ambiguous branches, and critical logic must feature inline code illustrations, ASCII flow diagrams, or detailed explanatory comments clarifying the intent and design for human maintainers.

---

## 7. Code Reviewer Approval Gate

- **Automated / Agent Review Gate**:
  - No code changes are committed or merged without passing audit by the `zvec-code-reviewer-expert` agent.
  - Reviews enforce TDD adherence, branch coverage, memory safety, AOT trimming annotations, SOLID compliance, <500-line class limits, zero dummy tests, and documentation completeness.

---

## 8. MkDocs Wiki Synchronous Updates

- Every feature, bug fix, architectural decision, script, or API modification must include corresponding updates to the `docs/` MkDocs wiki structure before PR completion.

---

## 9. CI Quality Gate & Agent Harness

- **Quality gate workflow**: [`.github/workflows/quality-gate.yml`](https://github.com/ahmedsamir50/AdamSystems.ZVec.NET-RAG/blob/main/.github/workflows/quality-gate.yml) — build, format, tests, line-count cap, dummy-test detection, AOT publish smoke.
- **Pre-commit hook**: [`.githooks/pre-commit`](https://github.com/ahmedsamir50/AdamSystems.ZVec.NET-RAG/blob/main/.githooks/pre-commit) — enable with `git config core.hooksPath .githooks`.
- **Agent rules**: [`.agents/AGENTS.md`](https://github.com/ahmedsamir50/AdamSystems.ZVec.NET-RAG/blob/main/.agents/AGENTS.md) — mandatory implementation loop (WRITE → TEST → REVIEW → VERIFY → DOC → MERGE).
- **ZVec.NET reference path**: resolve via `ZVEC_NET_REFERENCE_PATH` env var; fallback to NuGet `ZVec.NET` on CI/Linux/Mac.
