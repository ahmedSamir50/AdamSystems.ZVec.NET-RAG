# Ecosystem Watch & Dependency Matrix

This document tracks upstream Microsoft ecosystem packages, community signals, and strategic kill criteria for `ZVec.NET-RAG`.

---

> [!NOTE]
> **Implementation Status — Story 4.1 shipped**:
> `ZVec.Rag.LLamaSharp` and `ZVec.Rag.ONNX` recipe packages ship at `1.0.0-preview.1`. `DeterministicEmbedder` and `FakeChatClient` ship in `ZVec.Rag.Testing`. `SemanticTestEmbedder`, `IRagEvaluator`, and `DeterministicEvaluator` ship in Story 2.8. Roslyn SDK is pinned to `Microsoft.CodeAnalysis.CSharp` `4.12.0` and `Microsoft.Extensions.VectorData.Abstractions` `10.9.0` in CPM.

---

## 1. Upstream Microsoft Ecosystem & Tokenizer Dependencies

| Package | Purpose | Target Version |
|---|---|---|
| **`ZVec.NET`** | Native Embedded Vector DB Engine | `[1.0.0-beta.6, 2.0.0)` |
| **`Microsoft.Extensions.VectorData.Abstractions`** | Official Vector Store Abstractions (`IVectorStore`, `IVectorizedSearch<T>`) | `10.9.0` |
| **`Microsoft.Extensions.AI.Abstractions`** | Chat & Embedding Abstractions (`IChatClient`, `IEmbeddingGenerator`) | `10.9.0` |
| **`Microsoft.CodeAnalysis.CSharp`** | Roslyn Source Generator SDK | `4.12.0` |
| **`Microsoft.CodeAnalysis.Analyzers`** | Roslyn Analyzers SDK (`ZVec.Extensions.VectorData.Analyzers`) | `3.11.0` |
| **`Microsoft.ML.Tokenizers`** | Default Tokenizer Engine (Tiktoken BPE, SentencePiece, WordPiece) | `1.0.0+` |
| **`PdfPig`** | Optional PDF text extraction (`ZVec.Rag.Pdf`) | `0.1.16` |
| **`ZVec.Rag.Pdf`** | Optional PDF ingestion package | `1.0.0-preview.1` |
| **`ZVec.Rag.Template`** | `dotnet new zvec-rag` project templates | `1.0.0-preview.1` |
| **`ZVec.Rag.LLamaSharp`** | Local GGUF chat + embed adapters | `1.0.0-preview.1` |
| **`LLamaSharp`** | GGUF inference (transitive via `ZVec.Rag.LLamaSharp`) | `0.27.0` |
| **`LLamaSharp.Backend.Cpu`** | CPU backend for LLamaSharp | `0.27.0` |
| **`ZVec.Rag.ONNX`** | ONNX Runtime embedding adapters | `1.0.0-preview.1` |
| **`Microsoft.ML.OnnxRuntime`** | ONNX inference (transitive via `ZVec.Rag.ONNX`) | `1.22.1` |
| **`SixLabors.ImageSharp`** | CLIP image preprocessing | `3.1.12` |
| **`BenchmarkDotNet`** | Local allocation benchmarks (`ZVec.Rag.Benchmarks`) | `0.15.8` |
| **`tryAGI/Tiktoken`** | Optional BPE Adapter for high-throughput OpenAI Tiktoken workloads | `1.0.0+` |

---

## 2. Community Signals & Pivot Strategy Watchlist

| Signal / Issue | Community Need | Impact on ZVec.Rag | Status |
|---|---|---|---|
| **`microsoft/semantic-kernel#13224`** | Embedded LiteDB Vector Store proposal | Validates market demand for non-sqlite embedded vector store in .NET | 🟢 Open - No first-party embedded connector shipped |
| **`microsoft/agent-framework#1395`** | Persistent agent memory across sessions | Agent Framework lacks native embedded vector persistence | 🟢 Open - Opportunity for ZVec connector |
| **First-party Embedded Vector Store** | Microsoft shipping an official embedded `Microsoft.Extensions.VectorData` connector | **Pivot Strategy Trigger**: Strategic pivot to HNSW/IVF performance, native hybrid RRF, 9-RID mobile/MAUI support, and Native AOT trim safety | 🟢 None announced |

---

## 3. ZVec.NET Engine Baseline

- **NuGet Package**: [`ZVec.NET 1.0.0-beta.6`](https://www.nuget.org/packages/ZVec.NET/)
- **GitHub Repository**: [`ahmedSamir50/AdamSystems.ZVec.NET`](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET)
- **Engine Version**: `1.0.0-beta.6+zvec.0.6.0`
- **Supported RIDs (9 HARD)**: `win-x64`, `linux-x64`, `linux-arm64`, `osx-arm64`, `osx-x64`, `android-arm64`, `android-x64`, `ios-arm64`, `iossimulator-arm64`
- **Native AOT Status**: Verified 100% Native AOT clean under `PublishAot=true` via Phase 0 `ZVec.AotTestApp`.
