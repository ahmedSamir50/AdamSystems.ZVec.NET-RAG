# ZVec.Rag 🚀

> **Local-first RAG for .NET. No cloud. No Python. No kidding.**

`ZVec.Rag` is a high-performance, embedded, local-first Retrieval-Augmented Generation (RAG) framework built on top of [ZVec.NET](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET) and Microsoft's AI ecosystem primitives (`Microsoft.Extensions.VectorData` and `Microsoft.Extensions.AI`).

---

## ⚡ Key Features

- **No Cloud Required**: Runs 100% in-process on Windows, Linux, macOS, Android, and iOS. Zero monthly Azure/Qdrant vector DB bill.
- **`Microsoft.Extensions.VectorData` Connector**: First-class `IVectorStore` and `IVectorizedSearch<TRecord>` implementation backing ZVec.NET. Works seamlessly with Semantic Kernel, Microsoft Agent Framework, and community RAG tools.
- **`Microsoft.Extensions.AI` Ecosystem Integration**: Plug-and-play with any `IChatClient` or `IEmbeddingGenerator` (Ollama, Azure OpenAI, ONNX Runtime, LLamaSharp).
> **Status:** Planned for Phase 2 (Story 2.3 — Citation Tracking & SSE)
- **Streaming Citations (`IAsyncEnumerable<RagChunk>`)**: Real-time token streaming with precise document & page citation tracking (`SourceDoc`, `Page`, `Offset`, `Score`).
- **Universal Tokenization**: Universal `Microsoft.ML.Tokenizers` engine (Tiktoken BPE, SentencePiece for LLaMA 3 / Nomic, WordPiece for BERT) with pluggable support for `tryAGI/Tiktoken`.
> **Status:** Planned for Phase 2 (Story 2.2 — Document Ingestion)
- **Transparent Document Ingestion**: Core `ZVec.Rag` ships text/markdown readers only. PDF/HTML via optional `ZVec.Rag.Pdf` package. Chunking ACL wraps `Microsoft.Extensions.DataIngestion` preview (`ITextChunker` only; readers are separate).
> **Status:** Planned for Phase 2 (Story 2.3 — Hybrid Search Bridge)
- **Embedded Hybrid Search**: In-database dense + FTS (full-text search) retrieval with native Reciprocal Rank Fusion (`ZVecRrfReranker`).
- **Native AOT & Trimmer Friendly**: `ZVec.Extensions.VectorData` connector is Native AOT-verified (`ZVec.AotTestApp`, Phase 0). Full `ZVec.Rag` pipeline AOT is a **Phase 2 gate** (`ZVec.Rag.AotTestApp`, Story 2.7) — do not claim end-to-end pipeline AOT until that story passes.
- **Instant Scaffolding**: Get started in 60 seconds with `dotnet new rag`.

---

## 📦 Packages in this Repository & Cross-Navigation

| Package | Version | Primary Focus & Responsibility | Synergy & Cross-Navigation ("If you need X...") |
|---|---|---|---|
| **`ZVec.Extensions.VectorData`** | `0.1.0-alpha` | Official `Microsoft.Extensions.VectorData` connector for ZVec.NET | Core vector store (`IVectorStore`). *Need full RAG orchestration & citations? Add **`ZVec.Rag`**. Need zero-reflection AOT schemas? Add **`ZVec.Extensions.VectorData.SourceGenerator`**.* |
| **`ZVec.Rag`** | `0.5.0-alpha` | Batteries-included RAG orchestration (`IRagIngestor`, `IRagRetriever`, `IRagGenerator`, `IRagPipeline`, citations, SSE) | *Need pure vector storage for Semantic Kernel / Agent Framework? Use **`ZVec.Extensions.VectorData`**. Need unit test fakes without LLMs? Add **`ZVec.Rag.Testing`**. Running air-gapped without Ollama? Add **`ZVec.Rag.LLamaSharp`** or **`ZVec.Rag.ONNX`**.* |
| **`ZVec.Rag.Testing`** | `0.5.0-alpha` | Standalone unit testing fakes (`DeterministicEmbedder`, `SemanticTestEmbedder`, `FakeChatClient`, `IRagEvaluator`, `DeterministicEvaluator`) | *Add to test projects to mock RAG pipelines and measure Recall@K/MRR without external LLMs.* |
| **`ZVec.Rag.Template`** | `1.0.0-alpha` | Scaffolding project template for `dotnet new rag` | *Instantly scaffolds ASP.NET Core SSE, Console, or MAUI Blazor Hybrid apps powered by **`ZVec.Rag`**.* |
| **`ZVec.Rag.LLamaSharp`** | `1.1.0-alpha` | Recipe adapter for air-gapped local LLM execution via LLamaSharp (GGUF, Desktop Only) | *Combines with **`ZVec.Rag`** and **`ZVec.Extensions.VectorData`** for 100% offline, zero-network LLM RAG on Windows, Linux, and macOS.* |
| **`ZVec.Rag.ONNX`** | `1.1.0-alpha` | Recipe adapter for local ONNX embeddings & CLIP multimodal image processing | *Combines with **`ZVec.Rag`** to eliminate external network requests for vector embeddings.* |

---

## 💻 Honest Quickstart: Ingestion + Chat in 20 Lines

The quickstart demonstrates the **full RAG lifecycle**: Document Ingestion (`IngestTextAsync`) and Real-time SSE Streaming (`MapRagSseEndpoint`).

> **Status:** Planned for Phase 2 (Stories 2.1, 2.2, 2.3 — RAG Pipeline, Ingestion, SSE)
```csharp
using Microsoft.Extensions.AI;
using ZVec.Rag;

var builder = WebApplication.CreateBuilder(args);

// Register ZVec RAG pipeline with Microsoft.Extensions.AI components
builder.Services.AddZVecRag(opts => {
    opts.StoragePath = "./rag.zvec";
    opts.Embedder = ollama.Embeddings(model: "nomic-embed-text");
    opts.Chat = ollama.Chat(model: "llama3.2");
    opts.RrfK = 60; // Dense + FTS + RRF rerank (maps to ZVecHybridSearchOptions)
});

var app = builder.Build();

// 1. Ingest Text / Markdown Document (Auto-chunked by TokenTextChunker)
app.MapPost("/ingest", async (string text, string docId, IRagIngestor ingestor) => {
    await ingestor.IngestTextAsync(text, documentId: docId);
    return Results.Ok($"Document {docId} ingested successfully.");
});

// 2. Real-time Unbuffered SSE Streaming Chat Endpoint
// MapRagSseEndpoint links HttpContext.RequestAborted to AskAsync so client disconnect cancels generation.
app.MapRagSseEndpoint("/chat", async (string question, IRagGenerator generator, CancellationToken ct) =>
    generator.AskAsync(question, streamCitations: true, ct));

app.Run();
```

---

## 📄 Transparent Document Ingestion Architecture

Ingestion in `ZVec.Rag` is divided into pluggable pipeline stages aligned with `Microsoft.Extensions.DataIngestion`:

> **Status:** Planned for Phase 2 (Story 2.2 — Document Ingestion)
```
┌─────────────────────────┐    ┌─────────────────────────┐    ┌─────────────────────────┐    ┌─────────────────────────┐
│   1. Document Reader    │ -> │    2. Text Chunker      │ -> │  3. Vector Embedder     │ -> │  4. Persistent Store    │
│  (MD / TXT in core;     │    │ (Token / Markdown AST / │    │ (IEmbeddingGenerator<   │    │(ZVec.VectorData +       │
│   PDF via ZVec.Rag.Pdf) │    │  Sentence / Sliding)    │    │    string, Embedding>)  │    │     ZVec FTS Index)     │
└─────────────────────────┘    └─────────────────────────┘    └─────────────────────────┘    └─────────────────────────┘
```

- **Built-in Defaults**: Out-of-the-box `IngestTextAsync` handles plain text & markdown using token-boundary chunking (`TokenTextChunker`).
- **Advanced File Formats (PDF / HTML)**: Optional `ZVec.Rag.Pdf` package (`PdfDocumentReader`); not referenced by core `ZVec.Rag` or the AOT harness.
- **Explicit Chunking Strategies**: Developers can choose `TokenTextChunker` (512 tokens, 50 overlap), `MarkdownHeadingChunker`, or `SentenceTextChunker` based on document structure.

---

## 🔤 Tokenizer Strategy: `Microsoft.ML.Tokenizers` & `tryAGI/Tiktoken`

> **Status:** Planned for Phase 2 (Story 2.2 — Document Ingestion, Task 2.2.4)
- **Primary Tokenizer (`Microsoft.ML.Tokenizers`)**: Uses Microsoft's official high-performance, zero-allocation tokenizer engine. Supports **Tiktoken BPE** (OpenAI `cl100k`/`o200k`), **SentencePiece** (LLaMA 3, Nomic Embed), and **WordPiece** (BERT, MiniLM).
- **Pluggable Adapter (`tryAGI/Tiktoken`)**: Provides an optional high-throughput BPE adapter interface for developers targeting OpenAI models exclusively.

---

## 🌐 Ecosystem Architecture

> **Status:** Planned for Phase 2 (Stories 2.1, 2.2, 2.3, 2.6)
```
  ┌─────────────────────────────────────────────────────────────┐
  │                 Your .NET Application / API                 │
  └──────────────────────────────┬──────────────────────────────┘
                                 │
  ┌──────────────────────────────▼──────────────────────────────┐
  │                        ZVec.Rag                             │
  │   • IRagIngestor (Ingest/Chunk) • IRagRetriever (Retrieve)  │
│   • IRagGenerator (Chat/Stream) • IRagPipeline (Composite — no decorator middleware)  │
│   • Citation tracking           • Security Sanitizer        │
│   • MapRagSseEndpoint helpers   • ContextPacker (token budget) │
  └──────────────┬──────────────┬───────────────┬───────────────┘
                 │              │               │
  ┌──────────────▼──────┐  ┌────▼─────────────┐ │
  │ Microsoft.Extensions│  │ Microsoft.ML.    │ │
  │        .AI          │  │   Tokenizers     │ │
  │ • IChatClient       │  │ (BPE/Sentence- │ │
  │ • IEmbeddingGen     │  │     Piece)       │ │
  └──────────────┬──────┘  └──────────────────┘ │
                 │                              │
  ┌──────────────▼──────────────┐  ┌────────────▼───────────────┐
  │ Local / Cloud Models        │  │ ZVec.Extensions.VectorData│
  │ (Ollama / Azure / ONNX)     │  │ • IVectorStore             │
  │                             │  │ • IVectorizedSearch<T>     │
  └─────────────────────────────┘  └────────────┬───────────────┘
                                                │
                                   ┌────────────▼───────────────┐
                                   │ ZVec.NET Engine            │
                                   │ (In-process Native Vector) │
                                   └────────────────────────────┘
```

---

## 🛠️ Installation & Template Usage

```bash
# Install the project template
dotnet new install ZVec.Rag.Template

# Scaffold a new local RAG project
dotnet new rag -n MyLocalRagApp --llm ollama --embedder ollama
```

---

## 📜 License

This project is licensed under the [Apache-2.0 License](LICENSE).
