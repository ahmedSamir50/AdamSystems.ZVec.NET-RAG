# TDD & Testing Strategy

`ZVec.NET-RAG` mandates a strict Test-Driven Development (TDD) workflow across all components.

## Rules

1. **Red $\rightarrow$ Green $\rightarrow$ Refactor**: Unit tests must be written BEFORE writing implementation code.
2. **100% Branch Coverage**: All execution paths, edge cases, null checks, and error conditions must be covered.
3. **Mock-Free CI Execution**: Core RAG pipeline tests use `DeterministicEmbedder` and `FakeChatClient` in `ZVec.Rag.Testing` to execute in <100ms without downloading multi-GB LLMs.
4. **Snapshot Response Testing**: `Verify.Xunit` snapshot tests validate citation formats and prompt construction.
