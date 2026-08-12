# Ecosystem Watch & Dependency Matrix

This document tracks upstream Microsoft ecosystem packages, community signals, and strategic kill criteria for `ZVec.NET-RAG`.

---

## 1. Upstream Microsoft Ecosystem & Tokenizer Dependencies

| Package | Status | Ecosystem Role | Target Version |
|---|---|---|---|
| **`Microsoft.Extensions.VectorData`** | GA (May 2025) | Vector Store & Search Abstractions (`IVectorStore`, `IVectorizedSearch<T>`) | `9.0.0+` |
| **`Microsoft.Extensions.AI`** | GA (May 2025) | Chat & Embedding Abstractions (`IChatClient`, `IEmbeddingGenerator`) | `9.0.0+` |
| **`Microsoft.Extensions.DataIngestion`** | Preview (Dec 2025) | Ingestion & Document Chunking Pipeline | `9.0.0-preview*` |
| **`Microsoft.ML.Tokenizers`** | GA (Official) | Default Tokenizer Engine (Tiktoken BPE, SentencePiece, WordPiece) | `1.0.0+` |
| **`tryAGI/Tiktoken`** | OSS Community | Optional BPE Adapter for high-throughput OpenAI Tiktoken workloads | `1.0.0+` |
| **`Microsoft.AgentFramework`** | GA (April 2026) | Multi-Agent Orchestration & Shared Memory | `1.0.0+` |

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
