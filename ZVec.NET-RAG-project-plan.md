# ZVec.Rag — Project Plan (Path B)

> **Self-contained reference document.** No prior conversation context required. Written for a .NET lead architect deciding whether to build a batteries-included RAG starter kit on top of their existing ZVec.NET NuGet package. Includes refined architecture, backlog, competitor scan, technical pains, branding strategy, and a phased build plan.
>
> **Companion documents:**
> - `crdt-dotnet-project-plan.md` — alternative project (deferred)
> - `ZVec.NET-Project-Plan.md` and `ZVec.NET-Implementation-Plan.md` (in the ZVec.NET repo) — cover the vector DB SDK itself, not the RAG layer
>
> **Document version:** 2.0 — hardened with ground-truth findings from the actual ZVec.NET repo, NuGet page, samples folder, and demos repo (Aug 2026).

---

## 0. TL;DR

**Build a `Microsoft.Extensions.VectorData` connector for ZVec.NET as the v1 centerpiece, plus a batteries-included `ZVec.Rag` RAG orchestration layer that factors the RAG patterns already proven in sample code into a reusable, AOT-audited NuGet.** Ship a `dotnet new rag` template. Target the MAUI Blazor Hybrid flagship demo (already proven in the demos repo). Ride the local-first AI wave. 16–21 weeks to v1.0 (includes Phase 1.5 Risk Hardening Sprint and Phase 2 Contract Sprint).

**Why this project, in one sentence:**

> ZVec.NET already gives .NET the embedded vector database (no Qdrant, no pgvector, no Azure bill). Path B gives .NET the **Microsoft.Extensions.VectorData connector** that makes ZVec.NET a first-class citizen in the M.E.AI ecosystem, plus a **batteries-included RAG starter** that lifts proven sample patterns into a reusable library — so any .NET dev can do `dotnet new rag` and have a working local-first RAG app in 60 seconds.

---

## 1. What is this project?

Two new NuGet packages built on top of the existing ZVec.NET SDK:

### 1.1 `ZVec.Extensions.VectorData` — the centerpiece

A first-party-style `Microsoft.Extensions.VectorData` connector that backs `IVectorStore`, `IVectorizedSearch<TRecord>`, and `IVectorizableRecordCollection<TRecord, TKey>` with `IZvecCollection<T>` from ZVec.NET. Makes ZVec.NET consumable by every existing VectorData user — Semantic Kernel, Agent Framework, community RAG tooling — via `services.AddZVecVectorStore(...)`.

### 1.2 `ZVec.Rag` — the batteries-included starter

A batteries-included RAG orchestration layer that wires together:
- **M.E.VectorData** (vector store abstraction, GA May 2025)
- **M.E.AI** (`IChatClient`, `IEmbeddingGenerator`, GA May 2025)
- **M.E.DataIngestion** (chunking pipeline, Preview Dec 2025)
- **ZVec.Extensions.VectorData** (the connector above)
- Optional local LLM recipes (Ollama via M.E.AI, LLamaSharp adapter, ONNX Runtime adapter)

Plus a `dotnet new rag` template that scaffolds a working RAG app in 60 seconds.

**Working name:** `ZVec.Rag` (alt: `ZVec.RagKit`, `ZVec.RagStarter`). The connector package: `ZVec.Extensions.VectorData`.

---

## 2. Why does this need to exist? (The market gap)

### 2.1 The current .NET RAG landscape (verified Aug 2026)

| Layer | Status | Mature? |
|---|---|---|
| **LLM / embedding client abstraction** | `Microsoft.Extensions.AI` — GA May 2025 (`IChatClient`, `IEmbeddingGenerator`) | ✅ Mature (Microsoft) |
| **Vector store abstraction** | `Microsoft.Extensions.VectorData` — GA May 2025 | ✅ Mature (Microsoft) |
| **Chunking / ingestion pipeline** | `Microsoft.Extensions.DataIngestion` — Preview Dec 2025 (`IDocumentReader`, `ITextChunker`) | ⚠️ Preview, but paving is happening |
| **Tokenizer Engine** | `Microsoft.ML.Tokenizers` (Tiktoken BPE, SentencePiece, WordPiece) + pluggable `tryAGI/Tiktoken` | ✅ Mature (Microsoft / OSS) |
| **Orchestration / agents** | Microsoft Agent Framework — GA April 2026 (replaces SK + AutoGen) | ✅ Mature (Microsoft) |
| **Embedded persistent vector DB** | sqlite-vec (alpha, single maintainer), M.E.VectorData.InMemory (testing-only per Microsoft docs), LM-Kit.NET (closed-source commercial) | ❌ **NO mature OSS option** |
| **M.E.VectorData connector for ZVec.NET** | **Does not exist** | ❌ **Gap** |
| **Batteries-included RAG starter** | LangChain.NET (stale, April 2024), KernelMemory (deprecated), SmartRAG (single maintainer), Azure samples (educational only) | ❌ **NO mature OSS option** |
| **`dotnet new rag` template** | Does not exist | ❌ **Gap** |

### 2.2 Microsoft's own community is asking for this

- **`microsoft/semantic-kernel#13224`** (Oct 2025) — LiteDB Vector Store Connector proposal. Verbatim from the issue: *"The current preview SqliteVec connector works well for small local scenarios but inherits several limitations from sqlite-vec. Single-file persistence - ideal for local/offline RAG development."* The SK team's own community is on record saying sqlite-vec is too limited.
- **`microsoft/agent-framework#1395`** (Oct 2025) — Persistent agent memory request. The Agent Framework team has not built embedded persistence.

### 2.3 ZVec.NET already solves the hardest part

The vector DB itself is built and shipped. ZVec.NET v1.0.0-beta.5 (Aug 2026) provides:

- **9 HARD native RIDs**: win-x64, linux-x64, linux-arm64, osx-arm64, osx-x64, android-arm64, android-x64, ios-arm64, iossimulator-arm64 (+ maccatalyst-arm64 in pack, CI soft)
- **3 TFMs**: net8.0, net9.0, net10.0 (LTS floor: .NET 8)
- **Idiomatic .NET API**: `AddZVec()`, `AddZVecCollection<T>()`, `IZvecCollection<T>`, `[ZVecVector]`, `ReadOnlyMemory<float>` pin path, `SafeZvecHandle` lifecycle, `ZVecHealthCheck`, `IConfiguration` binding
- **Full engine surface**: HNSW, Flat, IVF, HNSW-RaBitQ (x86_64+AVX2 only), DiskANN (Linux only), Vamana, Invert, FTS indexes
- **Hybrid search**: dense+sparse with filter + RRF rerank, dense + FTS + weighted rerank, multi-vector + RRF
- **Rerankers**: `ZVecRrfReranker`, `ZVecWeightedReranker` — both in-DB natively
- **Schema evolution**: `EnsureSchema` (additive), `DropColumn`, `CreateIndex`, `Optimize`, `AddColumn`
- **Sync + async**: every op has both, async uses `ValueTask` + `CancellationToken`, throttles via `MaxConcurrentNativeCalls` / `MaxConcurrentReads`
- **Open modes**: `CreateAndOpen`, `Open`, `OpenOrCreate` (managed extension, DI default — restart-safe)
- **Published benchmarks**: 3.63 ms query / 6.9 KB alloc on 10k docs 768-d Flat; .NET beats Python (4.33 ms) and Node.js (4.10 ms)
- **Apache-2.0 license**, 139 MB NuGet, dual-published to nuget.org + GitHub Packages
- ** mkdocs docs site** at `ahmedsamir50.github.io/AdamSystems.ZVec.NET`

### 2.4 What's missing — the wedge Path B fills

| Missing piece | Why it matters | Difficulty |
|---|---|---|
| **`Microsoft.Extensions.VectorData` connector** | Unlocks ecosystem adoption (SK, AF, community RAG tools). Without this, ZVec.NET is an island. | Medium — well-defined conformance surface |
| **`Microsoft.Extensions.AI` integration** | Today samples hardcode LM Studio at `http://127.0.0.1:1234/v1`. M.E.AI integration lets users swap to Azure / OpenAI / Ollama / ONNX / LLamaSharp via DI. | Low — adapter pattern |
| **Factored RAG library** | Today RAG code lives scattered across 4 sample apps + 3 demos, each reinventing ingest/embed/cite/stream. | Medium — pattern extraction, not greenfield |
| **`dotnet new rag` template** | Distribution moat. Microsoft won't ship a ZVec template. | Low — template authoring |
| **AOT / trim audit** | ZVec.NET uses AOT-friendly patterns (`ReadOnlyMemory<float>` pin, SafeHandles) but has zero `[DynamicallyAccessedMembers]` annotations and zero published AOT verification. | Medium — annotation pass + CI test |
| **Cohesive story / branding** | ZVec.NET's repo has no GitHub topics, no tagline, no "no cloud, no Python" pitch. The story needs to be told. | Low — content work |

---

## 3. Target users

| Segment | Why they care | Size |
|---|---|---|
| **ISVs building local-first AI apps** (docs, notes, knowledge tools) | Want Linear/Obsidian-style "data stays on device" UX in .NET | Growing fast |
| **Enterprise architects in regulated industries** (healthcare, legal, finance, EU GDPR) | Cloud vector DBs = data residency violations. Need in-process RAG. | Large, underserved |
| **Edge / IoT teams** (factory floor, retail, field devices) | No server, no cloud, 50MB footprint, AOT-clean | Emerging |
| **Air-gapped environments** (defense, secure facilities) | Can't reach OpenAI/Azure. Need fully local RAG. | Niche but high-value |
| **Indie devs / startups** | Cost-sensitive. Azure AI Search bills scale with usage. Embedded = $0 infra. | Large |
| **Mobile-first ISVs (MAUI)** | On-device RAG, offline-first, no server round-trips | Growing |
| **Dev/test RAG** | `services.AddRag()` and go, no Qdrant container, no pgvector setup | Every .NET team doing RAG |
| **Microsoft.Extensions.AI ecosystem users** | Already using M.E.VectorData. Want an embedded persistent option. | Large (Microsoft is investing heavily here) |
| **Semantic Kernel / Agent Framework users** | Need persistent agent memory across sessions (issue #1395) | Growing with AF adoption |

---

## 4. Architecture

### 4.1 The critical wedge (don't drift from this)

```
                YOU BUILD (Path B v1)
                ─────────────────────
                ┌─────────────────────────────────────────────────────┐
                │  ZVec.Extensions.VectorData  (v1 CENTERPIECE)       │
                │  ───────────────────────────────────────────────    │
                │  • IVectorStore backed by IZvecFactory              │
                │  • IVectorizedSearch<TRecord> → IZvecCollection<T>  │
                │  • IVectorizableRecordCollection<TRecord, TKey>     │
                │  • Filter expression translator                     │
                │    (M.E.VectorData filter → ZVecFilterBuilder AST)  │
                │  • Source-generated record schemas                  │
                │    ([VectorStoreRecord] → [ZVecVector], [ZVecField])│
                │  • DI: services.AddZVecVectorStore(...)              │
                └─────────────────────────────────────────────────────┘
                                │
                ┌─────────────────────────────────────────────────────┐
                │  ZVec.Rag  (thin integration layer)                 │
                │  ───────────────────────────────────────────────    │
                │  • IRagPipeline orchestrator                        │
                │    (thin wrapper — delegates to M.E.AI + DataIngest)│
                │  • Ingestion: PDF/Word/MD/HTML → chunks             │
                │    (delegates to M.E.DataIngestion)                 │
                │  • Embedding: chunks → vectors                      │
                │    (delegates to M.E.AI IEmbeddingGenerator)        │
                │  • Storage: vectors → ZVec.NET                      │
                │    (via ZVec.Extensions.VectorData)                 │
                │  • Retrieval: query → top-K chunks (hybrid search)  │
                │  • Optional re-ranking hook (pluggable IReranker)   │
                │  • Generation: query + chunks → streaming answer    │
                │    (M.E.AI IChatClient)                             │
                │  • Citation tracking (chunk IDs → source + page)    │
                │  • Streaming IAsyncEnumerable<RagChunk>             │
                │  • Test fakes (deterministic embedder, fake chat)   │
                │  • DI: services.AddZVecRag(...)                     │
                └─────────────────────────────────────────────────────┘
                                │
                ┌─────────────────────────────────────────────────────┐
                │  ZVec.Rag.Template  (distribution)                  │
                │  ───────────────────────────────────────────────    │
                │  • dotnet new rag (Console / AspNet / Maui variants)│
                │  • Options: --llm ollama|azure|openai|llamasharp    │
                │  • Options: --embedder ollama|azure|onnx|llamasharp │
                └─────────────────────────────────────────────────────┘
                                │
                ┌─────────────────────────────────────────────────────┐
                │  Sample apps  (the viral demo — factored from       │
                │  existing samples, not greenfield)                  │
                │  ───────────────────────────────────────────────    │
                │  • "RAG your docs in 60 seconds" (Console)          │
                │  • "Local-first PDF chat" (ASP.NET Core + SSE)      │
                │  • "Offline phone RAG" (MAUI Blazor Hybrid)         │
                │  • "Air-gapped enterprise RAG" (AspNet + LLamaSharp)│
                │  • "Multimodal RAG" (CLIP ONNX — from demos repo)   │
                └─────────────────────────────────────────────────────┘

                YOU INTEGRATE WITH (don't reimplement)
                ──────────────────────────────────────
                ┌─────────────────────────────────────────────────────┐
                │  Microsoft.Extensions.AI          (GA May 2025)      │
                │  • IChatClient                                      │
                │  • IEmbeddingGenerator<string, Embedding<float>>    │
                │  Microsoft.Extensions.VectorData  (GA May 2025)      │
                │  • IVectorStore, IVectorizedSearch<T>                │
                │  Microsoft.Extensions.DataIngestion (Preview Dec 25)│
                │  • Chunking strategies (token, semantic-similarity) │
                │  • Embedding generation pipeline                    │
                │  Microsoft Agent Framework        (GA April 2026)    │
                │  • Orchestration patterns (optional, not required)  │
                └─────────────────────────────────────────────────────┘

                YOU ALREADY OWN (existing asset — Apache-2.0)
                ────────────────────────────────────────────
                ┌─────────────────────────────────────────────────────┐
                │  ZVec.NET  (1.0.0-beta.5, +zvec.0.6.0)              │
                │  • IZvecFactory / IZvecCollection<T>                │
                │  • AddZVec(), AddZVecCollection<T>()                │
                │  • [ZVecVector], [ZVecField], [ZVecId], [ZVecIgnore]│
                │  • Hybrid search + RRF/weighted rerankers (in-DB)   │
                │  • FTS, HNSW/Flat/IVF/Vamana/DiskANN indexes         │
                │  • SafeZvecHandle lifecycle, ZVecHealthCheck         │
                │  • 9 HARD native RIDs (Win/Linux/macOS/Android/iOS) │
                │  • net8.0 / net9.0 / net10.0                        │
                └─────────────────────────────────────────────────────┘
```

### 4.2 The cardinal rule

> **Integrate, don't reimplement.**
>
> Microsoft is paving the chunking/embedding/orchestration layer (M.E.AI GA, M.E.DataIngestion Preview, AF GA). If you reimplement these, Microsoft will commoditize that layer out from under you.
>
> Your wedge is:
> 1. The **M.E.VectorData connector** for ZVec.NET (unlocks ecosystem)
> 2. The **integration glue** that lifts proven sample patterns into a reusable library
> 3. The **`dotnet new rag` template** (distribution moat)
> 4. The **MAUI / offline / local-first story** (no competitor occupies this)

### 4.3 Package layout

```
ZVec.Extensions.VectorData         (v1 centerpiece — the bridge)
├─ VectorStore implementation (backed by IZvecFactory)
├─ VectorizedSearch<TRecord> (delegates to IZvecCollection<T>.Query)
├─ VectorizableRecordCollection<TRecord, TKey> (Insert/Upsert/Delete/Fetch)
├─ Score Semantics Converter (ZVec Cosine distance → M.E.VectorData Score similarity: 1.0 - dist)
├─ Filter expression translator
│  (M.E.VectorData filter expression → ZVecFilterBuilder AST; maps Enumerable.Contains → ContainAny)
├─ Source-generated record schemas
│  ([VectorStoreRecord] POCO → static schema builder AddField/AddVector calls + static mapper)
├─ Hybrid search bridge (VectorData "hybrid" → ZVec multi-query + ZVecRrfReranker)
├─ DI extensions: services.AddZVecVectorStore(...) (defaults MaxConcurrentNativeCalls = Environment.ProcessorCount)
├─ Conformance test suite (run against Microsoft's published contract)
└─ AOT/trim annotations on all public API

ZVec.Rag                           (v1 integration layer — the starter)
├─ IRagIngestor, IRagRetriever, IRagGenerator interfaces (SOLID Interface Segregation)
├─ IRagPipeline composite facade (delegates to ingestor, retriever, generator)
├─ Ingestion (wraps M.E.DataIngestion preview via IZVecTextChunker Anti-Corruption Layer)
├─ Embedder Stamp Manifest (zvec_index_manifest.json — validates model ID & dim consistency)
├─ Storage (via ZVec.Extensions.VectorData with ReaderWriterLockSlim handle management for Optimize reopen)
├─ Retrieval (hybrid: dense + FTS + ZVecRrfReranker, backed by ZVec)
├─ Security Sanitizer (IRagSecuritySanitizer — prompt injection mitigation)
├─ Context Window Budgeting (MaxContextTokens token packing via Microsoft.ML.Tokenizers)
├─ IReranker pluggable hook (default = ZVecRrfReranker for hybrid search)
├─ Generation (delegate to M.E.AI IChatClient with IList<ChatMessage> multi-turn support, streaming)
├─ Citation tracking (chunk IDs → source doc + page + offset + RankScore / DenseScore)
├─ RagChunk record (Text, Citations, IsFinal, Usage)
├─ IAsyncEnumerable<RagChunk> streaming
├─ DI extensions: services.AddZVecRag(...)
└─ Optional: ZVec.Rag.Ollama recipe (pre-wired Ollama via M.E.AI)

ZVec.Rag.Testing                   (v1 testing package — standalone)
├─ DeterministicEmbedder (random hash for fast pipeline unit tests)
├─ SemanticTestEmbedder (LSH for semantic rank order unit tests)
└─ FakeChatClient (dual streaming / non-streaming test chat client)

ZVec.Rag.LLamaSharp                (v1.1 — local LLM recipe, Desktop only)
├─ LLamaSharpChatClient : IChatClient
├─ LLamaSharpEmbedder : IEmbeddingGenerator<string, Embedding<float>>
└─ Recipe: fully local RAG (LLamaSharp + ZVec, zero network calls, Windows/Linux/macOS)

ZVec.Rag.ONNX                      (v1.1 — ONNX runtime recipe)
├─ OnnxEmbedder : IEmbeddingGenerator<...> (CLIP, MiniLM, EmbeddingGemma)
├─ ImagePreprocessor (SixLabors.ImageSharp NCHW normalization pipeline for CLIP)
└─ Recipe: multimodal RAG (text + image, see demos repo POC)

ZVec.Rag.Template                  (v1 distribution)
├─ dotnet new rag template (Console variant)
├─ dotnet new rag-aspnet template (ASP.NET Core + SSE streaming)
├─ dotnet new rag-maui template (MAUI Blazor Hybrid + offline)
├─ Template options: --llm, --embedder, --storage
└─ Published as ZVec.Rag.Template NuGet

ZVec.Rag.Samples                   (v1 viral demo — factored from existing)
├─ 01-rag-your-docs (Console, 60-second demo)
├─ 02-local-first-pdf-chat (ASP.NET Core + SSE, from samples/AspNet pattern)
├─ 03-offline-phone-rag (MAUI Blazor Hybrid, from demos/02-movie-recs pattern)
├─ 04-airgapped-enterprise-rag (AspNet + LLamaSharp, fully local)
├─ 05-multimodal-rag (CLIP ONNX, from demos/01-clip-onnx pattern)
└─ 06-aspire-dashboard (Aspire + Docker, from demos/Advanced/PDDM pattern)
```

### 4.4 Typical v1 code (the killer demo)

```csharp
// Program.cs — 20 lines, working local-first RAG
// (factored from samples/ZVec.NET.Samples.AspNet pattern)
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddZVecRag(opts => {
    opts.StoragePath = "./rag.zvec";
    opts.Embedder = ollama.Embeddings(model: "nomic-embed-text");
    opts.Chat = ollama.Chat(model: "llama3.2");
    opts.HybridSearch = true;        // dense + FTS + RRF (ZVec native)
    opts.CitationOrder = CitationOrder.ScoreDescending;
});

var app = builder.Build();

app.MapPost("/chat", async (string question, IRagPipeline rag, CancellationToken ct) => {
    await foreach (var chunk in rag.AskAsync(question, streamCitations: true, ct))
        await Response.WriteAsync(chunk.Text);
});

app.Run();
```

That's it. No Azure. No Python. No Qdrant. Single-file publish (AOT pending Phase 0 audit). The virality lives in this 20-line demo.

---

## 5. Backlog (epics)

### Epic 0 — Phase 0 preconditions (MUST complete before v1 work)

- [ ] **0.1 License-clean the demos repo** (`ZVec.Net-DemosAndPOCs` currently has no LICENSE file). Add Apache-2.0 to enable lifting patterns into the OSS starter.
- [ ] **0.2 AOT / trim audit of ZVec.NET**. Annotate public API with `[DynamicallyAccessedMembers]`. Add a `dotnet publish -c Release -r linux-x64 /p:PublishAot=true` CI job. Document which paths are AOT-clean and which need fixing. The pin-based `ReadOnlyMemory<float>` + SafeHandle design is favorable but unverified.
- [ ] **0.3 Confirm M.E.VectorData conformance test availability**. If Microsoft ships a conformance suite, run ZVec connector against it. If not, write one and contribute back.
- [ ] **0.4 Monitor `microsoft/semantic-kernel#13224` and `microsoft/agent-framework#1395`** for any first-party embedded connector announcement. Quarterly check.
- [ ] **0.5 Verify `Microsoft.Extensions.DataIngestion` API stability**. Currently Preview; abstract behind `IRagPipeline` so v1 doesn't break when DataIngestion goes GA.

### Epic 1 — `ZVec.Extensions.VectorData` connector (THE centerpiece)

- [ ] 1.1 `IVectorStore` implementation backed by `IZvecFactory` (collection-per-record-type model)
- [ ] 1.2 `IVectorizedSearch<TRecord>` delegating to `IZvecCollection<T>.Query`
- [ ] 1.3 `IVectorizableRecordCollection<TRecord, TKey>` (Insert / Upsert / Delete / Fetch)
- [ ] 1.4 `VectorStoreRecord` attribute → `[ZVecVector]` / `[ZVecField]` / `[ZVecId]` / `[ZVecIgnore]` mapping
- [ ] 1.5 Source-generated record schemas (AOT-clean, no reflection)
- [ ] 1.6 Filter expression translator: `VectorDataFilter → ZVecFilterBuilder AST`
  - Support `==`, `!=`, `<`, `<=`, `>`, `>=`, `&&`, `||`, `!`, `ContainAny`
  - Cover the 80% case in v1; document unsupported patterns
- [ ] 1.7 Hybrid search bridge: M.E.VectorData "hybrid" search → ZVec multi-query + `ZVecRrfReranker`
- [ ] 1.8 DI extensions: `services.AddZVecVectorStore(...)` (works alongside existing `AddZVec()`)
- [ ] 1.9 Conformance test suite (run against Microsoft's VectorData contract tests)
- [ ] 1.10 AOT/trim annotations + CI AOT publish test
- [ ] 1.11 Documentation: how to migrate from M.E.VectorData.InMemory to ZVec

### Epic 2 — `ZVec.Rag` integration layer

- [ ] 2.1 `IRagPipeline` orchestrator (thin wrapper)
- [ ] 2.2 `IRagIngestor` — delegates to `M.E.DataIngestion` for chunking (PDF/Word/MD/HTML)
- [ ] 2.3 `IRagEmbedder` — delegates to `M.E.AI IEmbeddingGenerator<string, Embedding<float>>`
- [ ] 2.4 `IRagRetriever` — hybrid search via `ZVec.Extensions.VectorData`
- [ ] 2.5 `IReranker` pluggable hook (default = identity; future: cross-encoder, LLM rerank)
- [ ] 2.6 `IRagGenerator` — delegates to `M.E.AI IChatClient`, streaming
- [ ] 2.7 `RagChunk` record (`Text`, `Citations`, `IsFinal`, `Usage`)
- [ ] 2.8 `Citation` record (`SourceDoc`, `Page`, `Offset`, `Score`, `ChunkId`)
- [ ] 2.9 Citation tracking: chunk IDs round-trip through embedding/retrieval/generation
- [ ] 2.10 Near-duplicate dedup (lift from existing samples pattern)
- [ ] 2.11 `IAsyncEnumerable<RagChunk>` streaming with cancellation
- [ ] 2.12 SSE endpoint helper (lift from `samples/AspNet` `/rag/ask/stream` pattern)
- [ ] 2.13 Test fakes: `DeterministicEmbedder` (hash-based vectors), `FakeChatClient`, `InMemoryRagPipeline`
- [ ] 2.14 Verify-based snapshot testing for RAG responses
- [ ] 2.15 DI extensions: `services.AddZVecRag(...)`

### Epic 3 — Local LLM recipes

- [ ] 3.1 `ZVec.Rag.Ollama` recipe (pre-wired Ollama via M.E.AI's `OpenAIClient.GetEmbeddingGenerator(...)`)
- [ ] 3.2 `ZVec.Rag.LLamaSharp` (LLamaSharpChatClient + LLamaSharpEmbedder as M.E.AI adapters)
- [ ] 3.3 `ZVec.Rag.ONNX` (OnnxEmbedder for CLIP / MiniLM / EmbeddingGemma — lift from `demos/01-clip-onnx`)
- [ ] 3.4 Recipe: fully local RAG (LLamaSharp + ZVec, zero network calls)
- [ ] 3.5 Recipe: multimodal RAG (CLIP ONNX + ZVec, see `demos/01-clip-onnx`)

### Epic 4 — `dotnet new rag` template

- [ ] 4.1 `dotnet new rag` (Console variant, ~50 LOC)
- [ ] 4.2 `dotnet new rag-aspnet` (ASP.NET Core + SSE streaming)
- [ ] 4.3 `dotnet new rag-maui` (MAUI Blazor Hybrid + offline)
- [ ] 4.4 Template options: `--llm ollama|azure|openai|llamasharp`, `--embedder ollama|azure|onnx|llamasharp`, `--storage zvec`
- [ ] 4.5 Published as `ZVec.Rag.Template` NuGet
- [ ] 4.6 Install docs: `dotnet new install ZVec.Rag.Template && dotnet new rag -n MyRagApp`

### Epic 5 — Sample apps (factored from existing, not greenfield)

- [ ] 5.1 **01-rag-your-docs** — Console, ingest a folder, ask questions (60-second demo)
- [ ] 5.2 **02-local-first-pdf-chat** — ASP.NET Core + SSE (lift from `samples/AspNet` `/rag/ask/stream` pattern, EN+AR + Egyptian FAQ fixtures)
- [ ] 5.3 **03-offline-phone-rag** — MAUI Blazor Hybrid (lift from `demos/02-movie-recs` MudBlazor pattern)
- [ ] 5.4 **04-airgapped-enterprise-rag** — AspNet + LLamaSharp + ZVec (zero network calls)
- [ ] 5.5 **05-multimodal-rag** — CLIP ONNX + ZVec (lift from `demos/01-clip-onnx` Flickr8k pattern)
- [ ] 5.6 **06-aspire-dashboard** — Aspire + Docker (lift from `demos/Advanced/PDDM` Jira RAG navigator pattern)
- [ ] 5.7 Each sample <200 LOC, each runnable in <60 seconds

### Epic 6 — Observability

- [ ] 6.1 `ActivitySource` per ingestion / retrieval / generation
- [ ] 6.2 Token usage tracking (wrap IChatClient + IEmbeddingGenerator with usage counters)
- [ ] 6.3 Latency histograms per pipeline stage
- [ ] 6.4 OTLP exporter config helpers
- [ ] 6.5 Verify-based snapshot testing for RAG responses
- [ ] 6.6 Integrate with existing `ZVecHealthCheck` for end-to-end health endpoints

### Epic 7 — Docs & branding

- [ ] 7.1 DocFX or mkdocs site (extend existing ZVec.NET mkdocs)
- [ ] 7.2 Quickstart: "RAG in 60 seconds" (the killer demo)
- [ ] 7.3 Architecture guide: "Why embedded?" + local-first AI manifesto
- [ ] 7.4 Comparison page: ZVec.Rag vs Azure AI Search vs sqlite-vec vs pgvector vs LM-Kit vs KernelMemory
- [ ] 7.5 Performance benchmarks vs sqlite-vec, M.E.VectorData.InMemory
- [ ] 7.6 Migration guides: from M.E.VectorData.InMemory → ZVec, from sqlite-vec → ZVec
- [ ] 7.7 Conference talk: "Local-first RAG in .NET: No Cloud, No Python, No Kidding"

### Epic 8 — Optional differentiators (post-v1)

- [ ] 8.1 Multi-modal RAG (text + image + audio — ZVec can store any vector)
- [ ] 8.2 Cross-device sync (your prior PostgreSQL↔SQLite sync engine experience transfers — RAG state sync between MAUI device and cloud)
- [ ] 8.3 Agent Framework integration sample (multi-agent shared vector memory — addresses `microsoft/agent-framework#1395`)
- [ ] 8.4 Schema migrations for evolving record types
- [ ] 8.5 Encrypted-at-rest storage (for regulated industries)
- [ ] 8.6 win-arm64 support (blocked by `alibaba/zvec#622` — unblock when MSVC issue resolved)

---

## 6. Technical pains & mitigations

| Pain | Severity | Mitigation | Mitigable? |
|---|---|---|---|
| **AOT / trim cleanliness unverified in ZVec.NET** | **CRITICAL** | Phase 0 precondition. Annotate public API. Add AOT publish CI job. Pin-based `ReadOnlyMemory<float>` + SafeHandle design is favorable. | ✅ Yes — you own ZVec.NET |
| **M.E.VectorData conformance** | High | Implement against official conformance contract. Microsoft's docs define the surface. If they ship a test suite, run it; if not, write one. | ✅ Yes |
| **M.E.DataIngestion still Preview** | Medium | Abstract behind `IRagPipeline`. When DataIngestion stabilizes, swap implementation. Document the seam. | ✅ Yes |
| **Filter expression translation** | Medium | M.E.VectorData filter expression → ZVecFilterBuilder AST. Cover `==`, `!=`, `<`, `<=`, `>`, `>=`, `&&`, `||`, `!`, `ContainAny` in v1. Standard visitor pattern. | ✅ Yes |
| **Hybrid search semantics** | Medium | ZVec already supports dense + FTS + RRF natively. Bridge: VectorData "hybrid" → ZVec multi-query + `ZVecRrfReranker`. Tunable weights. | ✅ Yes — engine already does it |
| **Blazor WASM not supported** | High | **Accepted constraint.** ZVec.NET has no native WASM RID and never will (native C++ core). MAUI Blazor Hybrid is the flagship instead — already proven in `demos/02-movie-recs`. | ✅ Yes — pivot done |
| **win-arm64 not shipped** | Low | Blocked by `alibaba/zvec#622` (MSVC CMake issue). Not blocking v1. Track upstream. | ⚠️ Track upstream |
| **Group-by query unsupported** | Low | `[Obsolete]`, throws `NotSupportedException` (upstream C API gap). Not needed for RAG v1. | ⚠️ Track upstream |
| **Native binary size (139 MB)** | Medium | Inherent to bundling C++ core (Arrow, FastPFOR, SIMDe). Document the trade-off. For MAUI, RID-specific publish trims unused platforms. | ⚠️ Accept |
| **Single-node scale ceiling** | Medium | **Author has already acknowledged this honestly** in the demos repo README: *"single-node scale (millions of vectors per machine). Planet-scale multi-tenant still belongs to managed cloud vector DBs."* Position v1 for single-node scenarios; multi-tenant via per-tenant ZVec instances. | ✅ Yes — honest positioning |
| **Concurrency during ingestion** | Medium | ZVec.NET already exposes `MaxConcurrentNativeCalls` / `MaxConcurrentReads` throttles. RAG pipeline uses `System.Threading.Channels` for backpressure. | ✅ Yes — SDK handles it |
| **Async = wrapper over sync P/Invoke** | Medium | ZVec.NET's async is "cooperative-cancel wrapper, not thread-pool offload" by explicit design. For RAG, this is fine — RAG is I/O-bound on the LLM call, not the vector search. Document the contract. | ✅ Yes — accepted |
| **Citation correctness** | Medium | Chunk IDs propagate through embed/retrieve/generate. Verify-based snapshot tests. Lift pattern from existing samples. | ✅ Yes |
| **Schema migrations for evolving record types** | Medium | ZVec.NET already has `EnsureSchema` (additive) + `DropColumn` + `CreateIndex`. Wrap with migration helpers. | ✅ Yes |
| **Streaming + cancellation** | Low | Standard `IAsyncEnumerable<T>` + `CancellationToken`. Lift from existing SSE pattern in samples. | ✅ Yes |
| **Token usage tracking** | Low | Wrap `IChatClient` / `IEmbeddingGenerator` with usage counters. | ✅ Yes |
| **Cold-start latency** | Low | ZVec index load on app start. Lazy load + background warmup. Snapshot format optimized for fast load. | ✅ Yes |
| **LM Studio dependency in samples** | Medium | Samples currently hardcode LM Studio at `http://127.0.0.1:1234/v1`. v1 samples should support any M.E.AI backend via DI. Provide a default Ollama recipe. | ✅ Yes |
| **Microsoft ships a first-party embedded VectorData connector** | Low (probability) / High (impact) | Mitigation: ship v1 within 6 months. ZVec's performance advantage (3.63 ms query vs sqlite-vec alpha) is defensible. Monitor `microsoft/semantic-kernel#13224` quarterly. | ⚠️ Track |

### Hard pains that could kill v1 if under-estimated

1. **AOT / trim audit** — the entire "embedded local-first RAG" wedge depends on ZVec.NET being AOT-clean. If the audit reveals deep reflection issues, you'll need to fix ZVec.NET first. **This is the Phase 0 precondition.**
2. **M.E.VectorData conformance** — failing the conformance contract means SK / AF / community tooling can't consume ZVec. Implement against the contract from day one.

---

## 7. Competitor landscape (released / in-progress / promised)

### 7.1 Released — embedded .NET vector DBs

| Library | License | Status | Mature? | Covers wedge? |
|---|---|---|---|---|
| **ZVec.NET** (yours) | Apache-2.0 | 1.0.0-beta.5 | Mature SDK, beta versioning | Foundation — Path B builds on this |
| **Microsoft.Extensions.VectorData.InMemory** | MIT | GA | Mature for testing; **explicitly not for production persistence** per Microsoft docs | ❌ |
| **sqlite-vec + SK connector** | MIT / Apache | Alpha upstream, Preview connector | Not mature | ⚠️ Partial, alpha-quality |
| **LanceDB .NET** | Apache | Single-contributor P/Invoke wrapper | Not mature | ⚠️ Partial |
| **ChromaDB.Client** | MIT | v1.0 but client-only (server mode) | N/A — not embedded | ❌ |
| **LM-Kit.NET** | Closed-source commercial | Mature (88k+ downloads) | Mature but **closed-source** — different market segment | ⚠️ Coexists, doesn't compete |

**Verdict:** No MATURE OSS embedded persistent .NET vector DB competitor. ZVec.NET is the strongest OSS option.

### 7.2 Released — .NET RAG starters

| Library | Status | Mature? | Covers wedge? |
|---|---|---|---|
| **tryAGI/LangChain.NET** | Stale (last release April 2024) | Not mature | ❌ |
| **Microsoft.KernelMemory** | Deprecated as legacy | Was mature, now deprecated | ❌ |
| **Microsoft.SemanticKernel Agent RAG** | Experimental (per Microsoft docs) | Not mature | ❌ |
| **Microsoft Agent Framework** | GA April 2026 | Mature for orchestration, not a RAG starter | ❌ |
| **SmartRAG** | v4.0.1, single maintainer | Not mature per strict definition | ⚠️ Partial |
| **Azure-Samples/semantic-kernel-rag-chat** | Educational sample, last commit Jan 2024 | Not a product | ❌ |
| **DNFileRAG, RAGSharp** | Niche / minimal | Not mature | ❌ |

**Verdict:** No MATURE OSS batteries-included .NET RAG starter exists.

### 7.3 In-progress / promised (Microsoft paving)

| Signal | Status | Impact on Path B wedge |
|---|---|---|
| **Microsoft.Extensions.DataIngestion** | Preview Dec 2025 | **Paves chunking/embedding pipeline.** Path B v1 MUST integrate, not reimplement. |
| **Microsoft.Extensions.VectorData new connectors** | Ongoing (Azure AI Search, Cosmos, pgvector, Qdrant, Redis) | **Microsoft has NOT announced an embedded persistent connector.** Wedge is open. |
| **`microsoft/semantic-kernel#13224`** | Open issue Oct 2025 | Microsoft's own community is asking for an embedded alternative to sqlite-vec. **Positive signal.** |
| **`microsoft/agent-framework#1395`** | Open issue Oct 2025 | AF team has not built embedded persistence. **Positive signal.** |
| **No `Microsoft.Extensions.Rag` namespace** | None announced | Microsoft is NOT shipping a first-party RAG starter. Wedge is open. |

### 7.4 Future threats

- **Microsoft ships a first-party embedded VectorData connector** (LiteDB or otherwise) — would partially commoditize the wedge. Mitigation: ship v1 within 6 months; build brand recognition before Microsoft moves. Track `microsoft/semantic-kernel#13224` quarterly.
- **Microsoft.Extensions.DataIngestion goes GA with a RAG starter sample** — would partially cover the integration layer. Mitigation: your starter is ZVec-specific and ships as `dotnet new rag` template (Microsoft won't ship a ZVec template).
- **LM-Kit.NET open-sources** — unlikely (commercial business model). If they do, they'd be a serious competitor.
- **A maintained sqlite-vec .NET wrapper emerges** — possible. Mitigation: ZVec's performance advantage (3.63 ms query vs alpha sqlite-vec) + your dual-package ownership is defensible.
- **`ydotnet`-style dead project revives** — not applicable to vector DBs. No equivalent threat.

**Verdict: Path B survives the strict kill rule. No mature OSS competitor covers the wedge.**

---

## 8. Viral potential — personal branding analysis

### 8.1 The story (use this verbatim in talks/blogs)

> "I built ZVec.NET — the first production-grade embedded vector database for .NET, on Alibaba's ZVec. Now I'm shipping the Microsoft.Extensions.VectorData connector that makes it a first-class citizen in the M.E.AI ecosystem, plus a batteries-included RAG starter that runs entirely in-process — no Azure, no server, no Python, no JavaScript. Run RAG on your phone, in your browser, on the factory floor, in the air-gapped enterprise. 100% .NET, Apache-2.0."

This story is **rare and defensible** because:

- **No one else occupies this seat.** Yjs authors own JS collab. Polly authors own resilience. **Nobody owns ".NET embedded AI."**
- **Local-first AI is the wave.** Linear, Obsidian, Notion-local — the movement is real. .NET has zero presence. You'd be the .NET local-first AI person.
- **Two-package reinforcement.** You own both ZVec.NET (the engine) and ZVec.Rag (the application layer). No competitor can match this without owning both pieces. Same structural advantage as the CRDT Vector CRDT idea — but here it's the *core* value proposition, not an optional Epic 7.
- **ZVec.NET benchmarks prove the engine.** 3.63 ms query, .NET beats Python and Node.js. Real numbers, published.
- **You have prior OSS shipping experience.** ZVec.NET is at beta.5 with published benchmarks, mkdocs site, dual-publish CI, provenance verification. This is not your first rodeo.

### 8.2 Realistic adoption curve

| Phase | Timeline | Stars | Trigger |
|---|---|---|---|
| Launch | Month 1 | 100–300 | ZVec.Rag launches; blog post; Reddit r/dotnet; HN |
| Early adoption | Months 2–4 | 500–1.5k | `dotnet new rag` template goes viral |
| Inflection | Months 4–9 | 1.5k–5k | Conference talk + "no cloud, no Python" pitch resonates |
| Growth | Year 2 | 5k–15k | Local-first AI wave crests; MAUI/Blazor Hybrid adoption grows |
| Steady-state | Year 3+ | 8k–25k | Default .NET choice for embedded RAG |

**Why higher ceiling than the CRDT project:** AI is a much bigger wave than collaboration. The addressable audience is every .NET dev touching AI, not just the collab-editing niche. The "no cloud" angle hits cost-sensitive, privacy-sensitive, and edge teams simultaneously.

**Existing traction baseline:** ZVec.NET is at 170 total downloads, 2 GitHub stars (as of Aug 2026). This is the floor — Path B's job is to drive adoption *of the ecosystem*, which lifts ZVec.NET with it.

### 8.3 Branding assets to build

- **Repo name** — `zvec-rag` (short, brand-aligned). Connector: `zvec-extensions-vectordata`.
- **Tagline** — "Local-first RAG for .NET. No cloud. No Python. No kidding."
- **Logo** — vector motif (arrows) merging into a database cylinder. Simple SVG, dark/light variants. Align with ZVec.NET's existing brand.
- **Demo site** — extend `ahmedsamir50.github.io/AdamSystems.ZVec.NET` with a "RAG in 60 seconds" interactive playground (live ASP.NET Core sample, Azure-hosted for demo purposes).
- **Talk abstracts** — 5 ready-to-submit:
  1. "Local-first RAG in .NET: No Cloud, No Python, No Kidding"
  2. "Building a Microsoft.Extensions.VectorData Connector: A Deep Dive"
  3. "On-device AI with MAUI and ZVec.NET"
  4. "Embedded Vector Search: Why SQLite-for-Vectors Is the Missing Piece"
  5. "Air-gapped Enterprise RAG: A .NET Architect's Guide"
- **YouTube** — short-form: "RAG in 60 seconds with ZVec.Rag" (60s), "Local-first RAG on your phone" (3min). Long-form: architecture walkthrough (20min).
- **Set GitHub topics on ZVec.NET repo** — currently none. Add: `vector-database`, `rag`, `dotnet`, `embeddings`, `local-first`, `microsoft-extensions-ai`, `hnsw`, `semantic-search`, `maui`, `edge-ai`.

### 8.4 CV ceiling

> "I designed and built ZVec.NET — the first production-grade embedded vector database for .NET (on Alibaba's ZVec, Apache-2.0). I then shipped the Microsoft.Extensions.VectorData connector and a batteries-included RAG starter (ZVec.Rag) that runs entirely in-process — no cloud, no Python, no JavaScript. X NuGet downloads, Y GitHub stars. Speaker at NDC / .NET Conf. Pioneered the local-first AI movement in .NET."

This demonstrates:
- **AI/ML infrastructure expertise** (vector DB internals, M.E.AI ecosystem)
- **Distributed systems experience** (your prior PostgreSQL↔SQLite sync engine transfers)
- **Modern .NET mastery** (M.E.AI ecosystem, AOT, SG, source-gen)
- **OSS leadership + ecosystem thinking** (two reinforcing packages, connector strategy)
- **Cross-platform native binary distribution** (9 RIDs, MAUI iOS/Android, Linux ARM)

It's also **the only candidate we've evaluated that leverages an existing asset** — every other candidate started from zero. Starting from a published, benchmarked, Apache-2.0 NuGet means you're 6+ months ahead of any competitor.

---

## 9. Build plan (phases)

### Phase 0 — Preconditions (1–2 weeks)

**MUST complete before v1 work begins:**

1. License-clean the demos repo (add Apache-2.0 to `ZVec.Net-DemosAndPOCs`)
2. AOT / trim audit of ZVec.NET (annotate public API, add AOT publish CI job)
3. Confirm M.E.VectorData conformance test availability
4. Monitor `microsoft/semantic-kernel#13224` and `microsoft/agent-framework#1395`
5. Verify M.E.DataIngestion API stability (abstract behind `IRagPipeline`)
6. Set GitHub topics on ZVec.NET repo
7. Set up `ZVec.Rag` repo, CI, NuGet publish pipeline

**Gate:** If AOT audit reveals deep issues that can't be fixed in 2 weeks, decide whether to ship v1 without AOT claims (positioned as "AOT support in v1.1") or fix ZVec.NET first.

### Phase 1 — `ZVec.Extensions.VectorData` connector (4–6 weeks)

- IVectorStore, IVectorizedSearch<TRecord>, IVectorizableRecordCollection<TRecord, TKey>
- Filter expression translator
- Source-generated record schemas
- Hybrid search bridge
- DI extensions
- Conformance test suite
- AOT/trim annotations + CI AOT publish test
- Documentation: migration from M.E.VectorData.InMemory

**Ship v0.1.0 to NuGet.** Blog post: "ZVec.NET is now a first-class Microsoft.Extensions.VectorData citizen."

### Phase 1.5 — Architectural Risk Hardening Sprint (2–3 weeks)

- **VectorData Score Normalization**: Convert ZVec Cosine distance to similarity (`1.0 - dist`).
- **Filter AST Visitor Expansion**: Pattern match `Enumerable.Contains` / `List.Contains` to `ZVecFilterBuilder.ContainAny`; throw explicit `ZVecFilterTranslationException` for unsupported LINQ operators.
- **iOS MonoAOT & SafeHandle Finalizer Interop Audit**: Run physical/simulator iOS test harness to ensure `zvec_collection_close` thread safety from finalizer.
- **Embedder Stamp Manifest & Schema Immutability Locking**: Write `zvec_index_manifest.json` on creation and validate on startup.

### Phase 2 — `ZVec.Rag` integration layer (4–5 weeks)

- **Week 11 (Contract Sprint)**: Finalize split interfaces (`IRagIngestor`, `IRagRetriever`, `IRagGenerator`), `ZVecRagSchemaV1` canonical schema, DI composition, and `ZVec.Rag.Testing` package.
- **Weeks 12–15**:
  - `IRagIngestor`, `IRagRetriever`, `IRagGenerator` implementation & `RagPipeline` composite facade
  - Ingestion (`M.E.DataIngestion` preview via `IZVecTextChunker` Anti-Corruption Layer) & deduplication (`OnDuplicate = Replace | Append | Skip`)
  - Storage & Reopen (`Optimize()` lifecycle managed via `ReaderWriterLockSlim`)
  - Retrieval (hybrid via ZVec connector, default `ZVecRrfReranker`, rich `HybridSearchOptions`)
  - Security Sanitizer (`IRagSecuritySanitizer` prompt injection filter)
  - Context Window Token Budgeting (`MaxContextTokens` via `Microsoft.ML.Tokenizers`)
  - Multi-turn conversation history (`IList<ChatMessage>`)
  - Citation tracking (chunk IDs → source doc + page + offset + `RankScore` / `DenseScore` distinction)
  - Streaming IAsyncEnumerable<RagChunk> with `app.MapRagSseEndpoint` unbuffered SSE helper
  - Standalone `ZVec.Rag.Testing` NuGet package (`DeterministicEmbedder`, `SemanticTestEmbedder`, `FakeChatClient`)
  - DI extensions: `services.AddZVecRag(...)`

**Ship v0.5.0.** Submit talk to NDC / .NET Conf.

### Phase 3 — `dotnet new rag` template + samples (3–4 weeks)

- `dotnet new rag` (Console, AspNet, MAUI variants)
- Template options (--llm, --embedder, --storage)
- Pre-embedded micro-fixture (100 pre-computed chunks) shipped with template for 60s working onboarding
- Sample 01: RAG your docs in 60 seconds (Console)
- Sample 02: Local-first PDF chat (AspNet + SSE)
- Sample 03: Offline phone RAG (MAUI Blazor Hybrid, INT8/INT4 quantized, EnableMmap=false)
- Sample 04: Air-gapped enterprise RAG (LLamaSharp, Desktop only)
- Sample 05: Multimodal RAG (CLIP ONNX + SixLabors.ImageSharp)
- Sample 06: Aspire dashboard

**Ship v1.0.0.** Conference talk delivered. "No cloud, no Python" manifesto blog post.

### Phase 4 — Local LLM recipes + polish (2–3 weeks)

- `ZVec.Rag.Ollama` recipe
- `ZVec.Rag.LLamaSharp` (LLamaSharpChatClient + LLamaSharpEmbedder, Desktop only)
- `ZVec.Rag.ONNX` (OnnxEmbedder for CLIP / MiniLM / EmbeddingGemma + ImagePreprocessor)
- Observability (ActivitySource, token tracking, OTLP)
- Docs site extension (quickstart, architecture, comparison, migration guides)
- Benchmark suite vs sqlite-vec, M.E.VectorData.InMemory

**Ship v1.1.0.**

### Phase 5 — Differentiators + ecosystem (ongoing from month 6)

- Agent Framework integration sample (addresses `microsoft/agent-framework#1395`)
- Cross-device sync (your PostgreSQL↔SQLite sync engine experience)
- Schema migrations (Sidecar SQLite metadata store for dynamic non-numeric fields)
- Encrypted-at-rest storage
- win-arm64 support (track `alibaba/zvec#622`)
- Conference talks (NDC, .NET Conf, DotNext)

**Total to v1.0: ~16–21 weeks of focused work.**

---

## 10. Commercial vs. OSS decision framework

### 10.1 Three viable paths

| Path | License | Revenue model | Pros | Cons |
|---|---|---|---|---|
| **Pure OSS (Apache-2.0, aligned with ZVec.NET)** | Apache-2.0 | None (sponsorships, consulting) | Maximum adoption, conference talks, name recognition | No direct revenue; burnout risk |
| **Open Core** | Apache core + commercial extensions | Paid: enterprise features (multi-tenant isolation, audit, RBAC, support) | Revenue + adoption; common pattern | Need clear free/paid line; community pushback risk |
| **Dual license (AGPL + commercial)** | AGPL | Commercial license for closed-source use | Revenue from enterprises; protects against cloud relaunch | Adoption drag (AGPL is scary); misaligned with ZVec.NET's Apache-2.0 |

### 10.2 Recommended split for Open Core (if commercialized)

**Free (Apache-2.0, aligned with ZVec.NET):**
- All of `ZVec.Extensions.VectorData` (the connector — must be free for ecosystem adoption)
- All of `ZVec.Rag` core (IRagPipeline, ingestion, retrieval, generation, streaming)
- In-memory + Ollama + LLamaSharp + ONNX recipes
- `dotnet new rag` template
- All samples

**Commercial (ZVec.Rag Pro):**
- Multi-tenant isolation (per-tenant ZVec instances with RBAC)
- Audit log + compliance hooks (for regulated industries)
- Encrypted-at-rest storage
- Cross-device sync engine (your prior art)
- SLA / support
- Enterprise recipes (Azure OpenAI with private endpoints, etc.)

**Rationale:** The free tier is enough for indie devs and startups (drives adoption). The paid tier targets enterprises building production local-first RAG (willing to pay for compliance and support).

### 10.3 Pricing guidance (if commercialized)

| Tier | Price | Target |
|---|---|---|
| Indie / OSS | Free | Hobbyists, students, indie devs |
| Pro (small team) | $99/mo per dev seat, or $499/mo unlimited | Startups building local-first SaaS |
| Enterprise | $2k–10k/mo + support | Mid-market and enterprise in regulated industries |
| Support contract | $25k+/yr | Production-reliant teams |

### 10.4 Decision criteria — when to commercialize

Commercialize **if and only if** at least 3 of these are true at the 12-month mark:

- [ ] ZVec.Rag has >2k GitHub stars within 12 months
- [ ] >5 production users (companies) willing to be referenced
- [ ] At least 1 enterprise asks for paid support / features
- [ ] You have bandwidth for sales / customer interactions (or partner with someone who does)
- [ ] The Pro Tier features are genuinely enterprise-only (not artificially crippled free tier)

If fewer than 3 are true at 12-month mark, **keep pure OSS** — the branding/CV value alone justifies the project.

---

## 11. Risks & kill criteria

### 11.1 Project risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| **AOT audit reveals deep issues in ZVec.NET** | Medium | Severe | Phase 0 precondition. If unfixable in 2 weeks, ship v1 without AOT claims (positioned as "AOT support in v1.1") |
| **Microsoft ships a first-party embedded VectorData connector** | Low | Fatal | Monitor `microsoft/semantic-kernel#13224` quarterly. Pivot to differentiate on performance + MAUI + local-first if announced. |
| **M.E.DataIngestion API churns when going GA** | Medium | Medium | Abstract behind `IRagPipeline`. Document the seam. Swap implementation when stable. |
| **Adoption stall (<500 stars at 12mo)** | Medium | Medium | Pure OSS pivot; focus on conference talks; double down on MAUI / local-first angle |
| **Author burnout** | Medium | Severe | Scope v1 tightly; defer multimodal/sync to v2; leverage existing samples (don't reinvent) |
| **LM-Kit.NET open-sources** | Low | High | Differentiate on MAUI + local-first + Microsoft ecosystem integration |

### 11.2 Pivot & Decision Criteria (re-evaluate at 3, 6, 12 months)

Execute strategic pivot (or pivot to maintenance-only) **if any**:

- Microsoft announces a first-party embedded VectorData connector $\rightarrow$ **Pivot Strategy**: Differentiate on HNSW/IVF performance, native hybrid RRF, 9-RID mobile/MAUI support, and Native AOT trim safety.
- After 6 months: <200 stars, <50 NuGet downloads/day, no conference talk accepted $\rightarrow$ pivot to maintenance.
- After 12 months: <1k stars, no production users $\rightarrow$ declare "learning project", stop active development.
- Author loses capacity $\rightarrow$ archive cleanly, write post-mortem blog.

---

## 12. References

### 12.1 ZVec.NET (your existing asset)

- **Repo:** https://github.com/ahmedSamir50/AdamSystems.ZVec.NET
- **NuGet:** https://www.nuget.org/packages/ZVec.NET/
- **Docs:** https://ahmedsamir50.github.io/AdamSystems.ZVec.NET
- **Samples:** https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/tree/main/samples
- **Demos & POCs:** https://github.com/ahmedSamir50/ZVec.Net-DemosAndPOCs
- **Version (Aug 2026):** 1.0.0-beta.5+zvec.0.6.0
- **License:** Apache-2.0
- **TFMs:** net8.0, net9.0, net10.0
- **Native RIDs (9 HARD):** win-x64, linux-x64, linux-arm64, osx-arm64, osx-x64, android-arm64, android-x64, ios-arm64, iossimulator-arm64 (+ maccatalyst-arm64 in pack)
- **Never supported:** Blazor WASM, HNSW-RaBitQ on ARM, DiskANN on non-Linux

### 12.2 Microsoft ecosystem (integrate, don't reimplement)

- **Microsoft.Extensions.AI** (GA May 2025) — https://learn.microsoft.com/dotnet/ai/
- **Microsoft.Extensions.VectorData** (GA May 2025) — https://learn.microsoft.com/dotnet/ai/conceptual/vector-data
- **Microsoft.Extensions.DataIngestion** (Preview Dec 2025) — https://devblogs.microsoft.com/dotnet/introducing-data-ingestion-building-blocks-preview
- **Microsoft Agent Framework** (GA April 2026) — https://learn.microsoft.com/agent-framework/

### 12.3 Community signals (positive — open gap confirmed)

- **`microsoft/semantic-kernel#13224`** — LiteDB Vector Store Connector proposal (Oct 2025). Microsoft's own community asking for an embedded alternative to sqlite-vec.
- **`microsoft/agent-framework#1395`** — Persistent agent memory request (Oct 2025).

### 12.4 Competitors (none mature enough to trigger kill rule)

- **sqlite-vec** — https://github.com/asg017/sqlite-vec (alpha, single maintainer)
- **Microsoft.Extensions.VectorData.InMemory** — testing-only per Microsoft docs
- **LM-Kit.NET** — https://lm-kit.com (closed-source commercial, 88k+ downloads)
- **tryAGI/LangChain.NET** — stale (last release April 2024)
- **Microsoft.KernelMemory** — deprecated as legacy

### 12.5 Implementation references

- **M.E.VectorData connector authoring guide** — https://learn.microsoft.com/dotnet/ai/conceptual/vector-data-implementing-a-connector
- **M.E.AI abstractions** — https://github.com/dotnet/extensions/tree/main/src/Libraries/Microsoft.Extensions.AI
- **Roslyn source generators** — https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/source-generators-overview
- **Native AOT** — https://learn.microsoft.com/dotnet/core/deploying/native-aot/
- **`dotnet new` template authoring** — https://learn.microsoft.com/dotnet/core/tools/custom-templates
- **BenchmarkDotNet** — https://benchmarkdotnet.org/
- **Verify** (snapshot testing) — https://github.com/VerifyTests/Verify

### 12.6 Prior art (the user's, transfers directly)

- **PostgreSQL ↔ SQLite sync engine** (private project) — experience transfers to cross-device sync differentiator (Epic 8.2)
- **ZVec.NET** (existing) — the foundation; Path B builds the layer above it
- **Existing RAG samples** (in ZVec.NET repo + demos repo) — patterns to factor into ZVec.Rag, not reinvent

---

## 13. Summary — go / no-go criteria

**Go if all true:**

- ✅ No mature OSS competitor covers the wedge (verified §7)
- ✅ No Microsoft first-party embedded VectorData connector announced (verified §7.3)
- ✅ Author has 12–16 weeks of focused bandwidth
- ✅ Author is willing to invest 1–2 weeks in Phase 0 preconditions (especially AOT audit)
- ✅ Author is willing to integrate with M.E.AI ecosystem, not reimplement
- ✅ Author accepts Blazor WASM is never supported (MAUI Blazor Hybrid is the flagship)

**No-go if any true:**

- ❌ AOT audit reveals deep issues in ZVec.NET that can't be fixed
- ❌ Microsoft announces a first-party embedded VectorData connector
- ❌ Author can't commit to 12–16 weeks of focused work
- ❌ Author wants to build "a better LangChain" (Microsoft paves that layer; can't win that war)

**If greenlit:** Start with Phase 0 (preconditions, 1–2 weeks). The AOT audit is the gating item. If AOT is clean (or fixable in the 2-week window), proceed to Phase 1 (the M.E.VectorData connector — the centerpiece).

---

## 14. New chat session handoff prompt

> If starting a new chat session to begin this project, paste the following:

```
I'm starting work on Path B from my prior research session. The full project plan is in
/home/z/my-project/download/ZVec.NET-RAG-project-plan.md — please read it completely
before responding.

Context: I own ZVec.NET (Apache-2.0, 1.0.0-beta.5, https://github.com/ahmedSamir50/AdamSystems.ZVec.NET).
Path B is to build two new NuGet packages on top of it:
1. ZVec.Extensions.VectorData — a Microsoft.Extensions.VectorData connector (the v1 centerpiece)
2. ZVec.Rag — a thin RAG integration library that factors proven sample patterns into a reusable library

Strict rules from prior session:
- Integrate with Microsoft.Extensions.AI / VectorData / DataIngestion / Agent Framework — NEVER reimplement
- MAUI Blazor Hybrid is the flagship demo (Blazor WASM is never supported by ZVec.NET)
- AOT/trim audit is a Phase 0 precondition
- Kill rule: if Microsoft announces a first-party embedded VectorData connector, kill immediately

I'm ready to start Phase 0. Begin with the AOT/trim audit of ZVec.NET and the demos repo
license-clean task. Don't proceed to Phase 1 until Phase 0 is complete.
```

---

*Document version 2.0. Self-contained — no prior conversation context required. Update competitor scan and Microsoft-paving watchlist quarterly. Track `microsoft/semantic-kernel#13224` and `microsoft/agent-framework#1395` monthly.*
