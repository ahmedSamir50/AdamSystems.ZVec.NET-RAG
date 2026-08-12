# Ecosystem Watch & Dependency Matrix

This document tracks upstream Microsoft ecosystem packages, community signals, and strategic kill criteria for `ZVec.NET-RAG`.

---

> [!NOTE]
> **Implementation Status Banner — Story 2.4 Complete**:
> Roslyn SDK is pinned to `Microsoft.CodeAnalysis.CSharp` `4.12.0` (matching .NET 9 SDK wave) and `Microsoft.Extensions.VectorData.Abstractions` `10.9.0` in CPM.

---

## 1. Upstream Microsoft Ecosystem & Tokenizer Dependencies

| Package | Purpose | Target Version |
|---|---|---|
| **`ZVec.NET`** | Native Embedded Vector DB Engine | `1.0.0-beta.5` |
| **`Microsoft.Extensions.VectorData.Abstractions`** | Official Vector Store Abstractions (`IVectorStore`, `IVectorizedSearch<T>`) | `10.9.0` |
| **`Microsoft.Extensions.AI.Abstractions`** | Chat & Embedding Abstractions (`IChatClient`, `IEmbeddingGenerator`) | `10.9.0` |
| **`Microsoft.CodeAnalysis.CSharp`** | Roslyn Source Generator SDK | `4.12.0` |
| **`Microsoft.CodeAnalysis.Analyzers`** | Roslyn Analyzers SDK | `3.11.0` |
| **`Microsoft.ML.Tokenizers`** | Default Tokenizer Engine (Tiktoken BPE, SentencePiece, WordPiece) | `1.0.0+` |
| **`tryAGI/Tiktoken`** | Optional BPE Adapter for high-throughput OpenAI Tiktoken workloads | `1.0.0+` |

---

## 2. Community Signals & Kill Criteria Watchlist

| Signal / Issue | Community Need | Impact on ZVec.Rag | Status |
|---|---|---|---|
| **`microsoft/semantic-kernel#13224`** | Embedded LiteDB Vector Store proposal | Validates market demand for non-sqlite embedded vector store in .NET | 🟢 Open - No first-party embedded connector shipped |
| **`microsoft/agent-framework#1395`** | Persistent agent memory across sessions | Agent Framework lacks native embedded vector persistence | 🟢 Open - Opportunity for ZVec connector |
| **First-party Embedded Vector Store** | Microsoft shipping an official embedded `Microsoft.Extensions.VectorData` connector | **Kill Rule Trigger**: Would trigger strategic pivot to performance & MAUI differentiation | 🟢 None announced |

---

## 3. ZVec.NET Engine Baseline

- **NuGet Package**: [`ZVec.NET 1.0.0-beta.5`](https://www.nuget.org/packages/ZVec.NET/)
- **GitHub Repository**: [`ahmedSamir50/AdamSystems.ZVec.NET`](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET)
- **Engine Version**: `1.0.0-beta.5+zvec.0.6.0`
- **Supported RIDs (9 HARD)**: `win-x64`, `linux-x64`, `linux-arm64`, `osx-arm64`, `osx-x64`, `android-arm64`, `android-x64`, `ios-arm64`, `iossimulator-arm64`
- **Native AOT Status**: Verified 100% Native AOT clean under `PublishAot=true` via Phase 0 audit harness.
