# ZVec.NET-RAG — Critical Technical Review (v2)

> **Scope of review:** Updated `project_tasks_implementation_plan.md` (Master Spec), `ZVec.NET-RAG-project-plan.md` (Strategic Plan v2.0), `README.md` (repo front page).
> **Cross-verified against:** ZVec.NET repo (`https://github.com/ahmedSamir50/AdamSystems.ZVec.NET`) — README, `src/Core`, `src/Native`, samples references.
> **Focus:** Architecture, design patterns, algorithms, AOT constraints, vector search semantics, RAG pipeline design, interop patterns, schema design.
> **Tone:** Adversarial, evidence-based, technical.
> **Date:** 2026-08-13.
> **Note:** This review contains only NEW findings not present in the prior review. Each finding is verified against the actual ZVec.NET codebase where the plan's claims can be checked.

---

## 0. Executive Summary

The updated docs harden the strategic narrative but introduce **new technical misconceptions** and leave several **architecture-level gaps** that will surface as production bugs in Phase 2. The most impactful findings cluster around three themes:

1. **The `IRagPipeline` orchestrator is over-loaded** — it conflates ingestion, retrieval, and generation behind one interface, violating the SOLID Interface Segregation Principle the plan itself mandates (Rule 3). The `AddZVecRag` options surface hides critical configuration (RRF `k`, dense/FTS weight ratio, index type selection) behind a single boolean.

2. **The source generator's relationship to ZVec.NET's reflection-based `ZVecCollectionSchemaBuilder.From<T>()` is undefined.** The plan claims "zero-reflection schemas" but ZVec.NET's own API is reflection-based at runtime. Either the generator duplicates ZVec.NET's schema logic (divergence risk) or it wraps the reflection call (the "zero-reflection" claim is false).

3. **Citation schema design is blocked by a ZVec.NET DDL constraint the plan does not acknowledge.** ZVec.NET's `EnsureSchema` only adds nullable numeric columns; string fields (`SourceDoc`, `ChunkId`) MUST be declared at create-time. This makes schema evolution for citation fields impossible without full re-ingestion — contradicting the plan's "schema migration" promise.

**Top 5 new critical findings (full list in §1–§9):**

1. **`IRagPipeline` violates ISP** — single interface for ingestion + query + streaming + cancellation; the summary mentions `IRagIngestor`/`IRagQuery` as separate but the spec only defines `IRagPipeline`.
2. **`ZVecCollectionSchemaBuilder.From<T>()` is reflection-based** — the source generator cannot honestly claim "zero reflection at runtime" while delegating to this API.
3. **`EnsureSchema` only adds nullable numeric columns** — citation string fields are create-time-only, breaking the schema migration story.
4. **Vector dimensionality is fixed at POCO level** (`[ZVecVector(768, ...)]`) — switching embedders (e.g., `nomic-embed-text` 768-d → `text-embedding-3-small` 1536-d) requires schema recreation and full re-ingestion.
5. **ZVec.NET CI tests only net8.0** — the net9.0 and net10.0 TFMs ship untested; the plan's "3 TFMs supported" claim is operationally weak.

**Verdict (full reasoning in §10):** **CONDITIONAL GO — but Phase 2 must be re-scoped to address the citation schema constraint, the source generator contract, and the `IRagPipeline` segregation before any RAG code is written.** These are not documentation fixes; they are architecture decisions that, if deferred, will require rewriting the Phase 2 deliverable.

---

## 1. RAG Pipeline Architecture

### Finding 1.1 — `IRagPipeline` violates Interface Segregation Principle

**Evidence:**
- README quickstart (lines 56–67) uses `IRagPipeline` for both `IngestTextAsync(text, documentId: docId)` and `AskAsync(question, streamCitations: true, ct)`.
- Master Spec Story 2.1.2: "Implement `RagPipeline : IRagPipeline` delegating to `Microsoft.Extensions.AI` ... Strictly cap class size <400 lines."
- Master Spec Non-Negotiable Rule 3: "Strict SOLID & 500-Line Class Limit."
- The session summary references `IRagPipeline`/`IRagIngestor`/`IRagQuery` as three separate interfaces, but the Master Spec only defines `IRagPipeline`.

**Why it matters:** A single `IRagPipeline` interface that exposes both `IngestTextAsync` and `AskAsync` forces every consumer to depend on both capabilities even when they only need one. An ingestion-only service (e.g., a background worker that ingests documents from a queue) must reference `AskAsync` and all its streaming machinery. A query-only service (e.g., a read-only chat endpoint) must reference `IngestTextAsync` and all its chunking/embedding machinery. This is the textbook ISP violation.

The `<400 lines` cap on `RagPipeline` is also at risk: an orchestrator that does ingestion + retrieval + generation + streaming + citation tracking + cancellation propagation in one class will hit 400 lines fast, forcing artificial decomposition (e.g., splitting into `RagPipeline.Read` and `RagPipeline.Write` partial classes) that doesn't actually reduce coupling.

**Concrete fix:**
- Split into three interfaces: `IRagIngestor` (`IngestTextAsync`, `IngestDocumentAsync`, `IngestBatchAsync`), `IRagRetriever` (`RetrieveAsync` — returns citations without LLM call), `IRagGenerator` (`AskAsync` — uses `IRagRetriever` internally). `IRagPipeline` becomes a marker interface that composes all three for convenience.
- The `RagPipeline` class becomes a facade that delegates to `RagIngestor`, `RagRetriever`, `RagGenerator` — each under 200 lines, each independently testable.
- Document the segregation in `docs/architecture/rag-pipeline.md`.

**Owner:** `zvec-rag-pipeline-expert`. **Effort:** 1 day (interface design before implementation).

---

### Finding 1.2 — `AddZVecRag` vs `AddZVecVectorStore` DI composition undefined

**Evidence:**
- README quickstart (line 46): `builder.Services.AddZVecRag(opts => { opts.StoragePath = "./rag.zvec"; ... })`.
- Master Spec Story 1.7.4: "Implement `ZVecVectorStoreServiceCollectionExtensions`" — `services.AddZVecVectorStore(...)`.
- Strategic plan §4.3: "DI extensions: `services.AddZVecVectorStore(...)` (works alongside existing `AddZVec()`)".
- ZVec.NET README: `AddZVec()` and `AddZVecCollection<T>()` are the underlying registrations.

**Why it matters:** There are now three DI extension methods layered:
1. `AddZVec()` (ZVec.NET — registers `IZvecFactory`)
2. `AddZVecVectorStore(...)` (ZVec.Extensions.VectorData — registers `IVectorStore` backed by `IZvecFactory`)
3. `AddZVecRag(...)` (ZVec.Rag — registers `IRagPipeline` backed by `IVectorStore` + `IChatClient` + `IEmbeddingGenerator`)

The composition rules are undefined:
- Does `AddZVecRag` internally call `AddZVecVectorStore` and `AddZVec`? If yes, the user cannot customize the underlying `ZVecOptions` (log level, throttles) from `AddZVecRag`'s options.
- If no, the user must call all three in order — but the `StoragePath` must be consistent across all three. The plan doesn't show how.
- What happens if the user calls `AddZVecVectorStore` then `AddZVecRag` with a different `StoragePath`? Silent override? Exception? Two ZVec instances pointing at different files?

**Concrete fix:**
- Define the composition contract: `AddZVecRag` accepts a `ZVecRagOptions` that contains a nested `ZVecOptions` and `ZVecVectorStoreOptions`. It calls `AddZVec` and `AddZVecVectorStore` internally with those nested options.
- Add a conformance test: registering `AddZVecRag` produces exactly one `IZvecFactory` singleton, one `IVectorStore` singleton, one `IRagPipeline` (lifecycle TBD — see Finding 1.6).
- Document the layered DI in `docs/architecture/di-composition.md` with a sequence diagram.

**Owner:** `zvec-rag-pipeline-expert` + `zvec-vectordata-expert`. **Effort:** 4 hours.

---

### Finding 1.3 — `ZVecOptions` vs `RagOptions` composition undefined

**Evidence:**
- ZVec.NET README: `factory.Initialize(new ZVecOptions { LogLevel = ZVecLogLevel.Warn })`.
- ZVec.NET README: `MaxConcurrentNativeCalls` / `MaxConcurrentReads` throttles.
- README quickstart: `opts.StoragePath`, `opts.Embedder`, `opts.Chat`, `opts.HybridSearch`, `opts.CitationOrder` (strategic plan §4.4).
- No doc shows how `ZVecOptions.LogLevel`, `ZVecOptions.MaxConcurrentNativeCalls`, or `ZVecOptions.MaxConcurrentReads` are exposed through `AddZVecRag`.

**Why it matters:** The RAG pipeline's ingestion throughput is bottlenecked by `MaxConcurrentNativeCalls` (sync P/Invoke throttle). If `AddZVecRag` doesn't expose this, the user cannot tune ingestion performance without bypassing `AddZVecRag` and calling `AddZVec` directly — defeating the "batteries-included" promise.

Similarly, `LogLevel` affects observability. If `AddZVecRag` silently sets `LogLevel = Warn`, the user can't enable `Info` or `Debug` for troubleshooting without knowing about the underlying `ZVecOptions`.

**Concrete fix:**
- `ZVecRagOptions` must expose a `ZVec ZVecOptions { get; set; }` nested property (or use a configurer callback `configure => configure.MaxConcurrentNativeCalls = 8`).
- Document the default values: `MaxConcurrentNativeCalls = Environment.ProcessorCount` (or whatever ZVec.NET's default is), `MaxConcurrentReads = ...`, `LogLevel = Warn`.
- Add an XML doc on `AddZVecRag` showing how to override: `services.AddZVecRag(opts => { opts.ZVec.MaxConcurrentNativeCalls = 16; })`.

**Owner:** `zvec-rag-pipeline-expert`. **Effort:** 2 hours.

---

### Finding 1.4 — `IngestTextAsync` deduplication semantics undefined

**Evidence:**
- README quickstart (lines 56–59): `app.MapPost("/ingest", async (string text, string docId, IRagPipeline rag) => { await rag.IngestTextAsync(text, documentId: docId); ... })`.
- Master Spec Story 2.2.2: "Implement `RagIngestor : IRagIngestor` wrapping `Microsoft.Extensions.DataIngestion` preview with pluggable `IDocumentReader` and `ITextChunker`."
- No doc specifies what happens when the same `documentId` is ingested twice.

**Why it matters:** In production, users will re-upload the same document (e.g., a user re-ingests `policy.pdf` after editing it). Three possible semantics:
1. **Replace** — delete existing chunks for `docId`, then insert new chunks. Requires a `DeleteByDocId` operation (does ZVec.NET support filtered delete? The plan doesn't say).
2. **Append** — insert new chunks alongside old chunks. Results in duplicate content in retrieval, degrading RAG quality.
3. **Error** — throw `DuplicateDocumentException`. Forces the user to manually delete before re-ingesting.

The plan picks none. The default behavior will be discovered at runtime by the first user who re-ingests a document — and it will likely be "append" (the path of least resistance for the implementer), which is the wrong default.

**Concrete fix:**
- Define `IngestOptions` with `OnDuplicate` enum: `Replace` (default), `Append`, `Skip`.
- For `Replace`: implement `DeleteByDocId` using ZVec's filter API (`filter: c => c.SourceDoc == docId`) followed by batch delete. Document the performance characteristic (filter + delete is O(n) over the collection).
- Add a Story 2.2.5: "Document Deduplication & Re-ingestion Semantics" with tests for all three modes.
- Update the README quickstart to show the default explicitly: `await rag.IngestTextAsync(text, documentId: docId, options: new() { OnDuplicate = DuplicateMode.Replace });`.

**Owner:** `zvec-rag-pipeline-expert`. **Effort:** 1 day.

---

### Finding 1.5 — `AskAsync` conversation history absent

**Evidence:**
- README quickstart (line 62): `await foreach (var chunk in rag.AskAsync(question, streamCitations: true, ct))`.
- Master Spec Story 2.3.2: `RagChunk` record has `Text`, `Citations`, `IsFinal`, `Usage`. No `ConversationId`, no `History`.
- Strategic plan §4.3: "Generation: query + chunks → streaming answer (M.E.AI IChatClient)". No mention of multi-turn.

**Why it matters:** RAG without conversation history is single-shot Q&A. Real RAG applications (chatbots, assistants) require multi-turn: "Tell me about X" → "Can you elaborate on the second point?" The second query must be answered in the context of the first.

`Microsoft.Extensions.AI`'s `IChatClient.GetResponseAsync` takes `IList<ChatMessage>` — a conversation. The plan's `AskAsync(string question, ...)` takes a single string, throwing away the conversation abstraction.

The implication: users who need multi-turn RAG must bypass `IRagPipeline.AskAsync` and call `IChatClient` directly, reimplementing retrieval + citation tracking. This defeats the "batteries-included" promise.

**Concrete fix:**
- Add `AskAsync(IList<ChatMessage> history, string question, ...)` overload.
- Or: add `AskAsync(RagQuery query, ...)` where `RagQuery` has `Question`, `History`, `Filter`, `TopK`, `RerankerOptions`.
- Document the conversation storage strategy: does `IRagPipeline` persist conversation history? If yes, where (ZVec sidecar table? In-memory? Cache?)? If no, the user must persist and pass history each call.
- Add a Story 2.1.4: "Multi-turn Conversation Support" with tests for conversation-aware retrieval (e.g., "Can you elaborate on point 2" must retrieve chunks related to point 2 of the previous answer, not parse "point 2" as a standalone query).

**Owner:** `zvec-rag-pipeline-expert`. **Effort:** 1 day.

---

### Finding 1.6 — `opts.HybridSearch = true` boolean hides rich reranker configuration

**Evidence:**
- README quickstart (line 50): `opts.HybridSearch = true; // Dense + FTS + RRF rerank (ZVec native)`.
- Strategic plan §4.4: adds `opts.CitationOrder = CitationOrder.ScoreDescending;`.
- ZVec.NET README: "Rerankers: `ZVecRrfReranker`, `ZVecWeightedReranker` — both in-DB natively" and "Hybrid search: dense+sparse with filter + RRF rerank, dense + FTS + weighted rerank, multi-vector + RRF".
- Master Spec Story 1.7.2: "Implement `IKeywordHybridSearchable<TRecord>` bridge in `ZVecVectorizableRecordCollection`."

**Why it matters:** A boolean `HybridSearch = true` exposes none of:
- RRF `k` constant (conventional 60, but tunable; affects ranking).
- Dense vs FTS weight ratio (50/50? 70/30? user-tunable?).
- Which reranker: `ZVecRrfReranker` vs `ZVecWeightedReranker`?
- Top-K per retriever (dense top-100, FTS top-100, fused top-10? Or dense top-50, FTS top-50, fused top-10?).
- Whether filter is applied before retrieval (ZVec native) or after fusion (different semantics).

The user who sets `HybridSearch = true` gets whatever defaults the implementer picked — and has no way to tune retrieval quality without diving into ZVec.NET's native API, bypassing the RAG layer.

**Concrete fix:**
- Replace `bool HybridSearch` with `HybridSearchOptions? HybridSearch` (null = disabled):
  ```csharp
  public class HybridSearchOptions {
      public RerankerType Reranker { get; set; } = RerankerType.RRF;
      public int RrfK { get; set; } = 60;
      public double DenseWeight { get; set; } = 0.7;
      public double FtsWeight { get; set; } = 0.3;
      public int TopKPerRetriever { get; set; } = 100;
      public FilterApplicationPoint FilterApplication { get; set; } = FilterApplicationPoint.PreRetrieval;
  }
  ```
- Document the defaults and the validation (e.g., `DenseWeight + FtsWeight` must equal 1.0 for weighted reranker; RRF ignores weights).
- Add tests that verify different reranker configurations produce different rankings on the same dataset.

**Owner:** `zvec-rag-pipeline-expert` + `zvec-vectordata-expert`. **Effort:** 1 day.

---

## 2. VectorData Connector Design

### Finding 2.1 — `ZVecCollectionSchemaBuilder.From<T>()` is reflection-based; source generator relationship undefined

**Evidence:**
- ZVec.NET README: `var schema = ZVecCollectionSchemaBuilder.From<Product>().Build();` — runtime schema construction from POCO via reflection over `[ZVecVector]`, `[ZVecField]`, `[ZVecId]` attributes.
- Master Spec Story 1.6.2–1.6.3: "Implement `ZVecRecordMetadataGenerator : IIncrementalGenerator` inspecting `[VectorStoreRecord]` attributes. Emit zero-reflection static metadata mappers (`IVectorRecordMapper<TRecord>`)."
- Master Spec Story 1.6 acceptance: "AOT-clean schema generation with 0 reflection at runtime."
- Master Spec Verification Matrix: "Source Generator | CodeGen Unit Test | Roslyn Test Kit | 0 runtime reflection".

**Why it matters:** The plan claims the source generator emits "zero-reflection" mappers, but ZVec.NET's own schema construction (`ZVecCollectionSchemaBuilder.From<T>()`) is reflection-based. The connector must call `ZVecCollectionSchemaBuilder.From<T>()` at some point to register the schema with ZVec.NET's native engine — there's no alternative API. So one of these is true:

1. **The source generator emits a static schema object that bypasses `ZVecCollectionSchemaBuilder.From<T>()`.** This requires ZVec.NET to expose a schema-building API that accepts pre-computed metadata (not a POCO type). If ZVec.NET doesn't expose this, the source generator can't bypass reflection — it can only pre-compute metadata that's then fed to the reflection-based builder (still reflection).
2. **The source generator emits a mapper (`IVectorRecordMapper<TRecord>`) that's separate from the schema.** The schema still uses reflection; only the record-to-`ZVecDoc` mapping is source-generated. The "0 runtime reflection" claim then applies only to mapping, not to schema — misleading.
3. **The source generator is vaporware** — it emits nothing useful, and the connector uses `ZVecCollectionSchemaBuilder.From<T>()` directly with full reflection.

The plan doesn't specify which. Without this clarity, the AOT claim is unverified.

**Concrete fix:**
- Audit ZVec.NET's API: does it expose a schema-building entry point that accepts pre-computed `ZVecFieldInfo[]` instead of a `Type`? If yes, the source generator can emit the field array at compile time and bypass reflection. If no, the AOT claim is false until ZVec.NET adds this API.
- Clarify in the README: "The source generator emits zero-reflection **record mappers**. Schema construction uses ZVec.NET's `ZVecCollectionSchemaBuilder.From<T>()`, which is reflection-based. AOT compatibility is achieved because the reflection occurs at schema-build time (first call), not on the query hot path."
- Update the Verification Matrix: "Source Generator | CodeGen Unit Test | 0 reflection on **query hot path** (schema build uses reflection at startup)".
- If true AOT (no reflection anywhere) is required, file an issue against ZVec.NET to expose a non-reflective schema API. Track in `docs/reference/aot-roadmap.md`.

**Owner:** `zvec-vectordata-expert` + `zvec-native-aot-expert`. **Effort:** 1 day (API audit + clarification).

---

### Finding 2.2 — `IZvecCollection<T>` (typed) vs `IZvecCollection` (dynamic) — connector uses which?

**Evidence:**
- ZVec.NET README: "Two APIs: Typed (recommended) — `IZvecCollection<T>`, `ZVecCollectionSchemaBuilder.From<T>()`, `AddZVecCollection<T>`, expression filters. Dynamic (escape hatch) — `IZvecCollection`, `ZVecDoc`, string field names, `ZVecFilterBuilder.Where("…")`. Typed is a thin façade over dynamic (`IZvecCollection<T>.Untyped`)."
- Master Spec Story 1.4.2: "Implement `ZVecVectorizableRecordCollection<TRecord, TKey> : IVectorStoreRecordCollection<TKey, TRecord>`."
- Master Spec Story 1.5.2: "Implement `ZVecFilterExpressionVisitor` AST translator returning `ZVecFilterBuilder`."

**Why it matters:** The M.E.VectorData connector receives a generic `TRecord` at compile time. The natural mapping is to `IZvecCollection<TRecord>` (typed). But:

- `ZVecFilterBuilder` (returned by the visitor) is the **dynamic** API's filter type. The typed API uses expression filters (`p => p.Category == "foo"`).
- If the connector uses `IZvecCollection<TRecord>.Query(p => p.Embedding, queryVec, filter: ...)` — what's the `filter` parameter type? If it's an expression tree, the visitor must emit an expression tree, not a `ZVecFilterBuilder`.
- If the connector uses `IZvecCollection<TRecord>.Untyped.Query(...)` with a `ZVecFilterBuilder`, it bypasses the typed API's type safety.

The plan's `ZVecFilterExpressionVisitor` returns `ZVecFilterBuilder` (dynamic), but the connector holds `IZvecCollection<TRecord>` (typed). The interop between these two layers is undefined. The likely implementation is `collection.Untyped.Query(vec, topK, filter: visitor.Visit(filterExpr))` — which works but throws away the typed API's compile-time field safety, making the source generator's typed mapper less useful.

**Concrete fix:**
- Decide: does the connector use the typed API (expression-tree filter) or the dynamic API (`ZVecFilterBuilder`)?
- If dynamic: document that the typed API's expression filters are not used because M.E.VectorData's filter expression is a different LINQ shape than ZVec.NET's typed filter expression. The visitor translates M.E.VectorData → `ZVecFilterBuilder` (dynamic).
- If typed: the visitor must emit a ZVec typed expression (`Expression<Func<TRecord, bool>>`), which requires rewriting the M.E.VectorData expression tree into a ZVec-compatible expression tree. This is more complex but preserves type safety.
- Either way, document the decision and its tradeoffs in `docs/architecture/filter-translation.md`.

**Owner:** `zvec-vectordata-expert`. **Effort:** 4 hours (decision + doc).

---

### Finding 2.3 — `IZvecFactory` DI lifetime undefined (Singleton required for native handle)

**Evidence:**
- ZVec.NET README: `using var factory = new ZVecFactory(); factory.Initialize(...);` — factory owns native `zvec` handle via `SafeZvecHandle`.
- ZVec.NET README: "Shutdown disposes all tracked open collections before `zvec_shutdown`" — factory tracks all collections.
- Master Spec Story 1.3.2: "Implement `ZVecVectorStore : IVectorStore` backed by `IZvecFactory`."
- No doc specifies DI lifetime for `IZvecFactory`, `IVectorStore`, or `IZvecCollection<T>`.

**Why it matters:** `IZvecFactory` holds a native `zvec` engine handle. If registered as `Scoped` or `Transient`, each request creates a new native engine — exhausting file descriptors and native memory within hundreds of requests. It MUST be `Singleton`.

`IVectorStore` (backed by `IZvecFactory`) is also `Singleton` — it's a stateless façade.

`IZvecCollection<T>` is more nuanced:
- If `Singleton`: all requests share one collection handle. Concurrent reads OK; concurrent writes serialize through `MaxConcurrentNativeCalls`.
- If `Scoped`: each request opens its own collection handle (via `factory.Open(path)`). This multiplies native handles by the request count — wasteful and may hit ZVec's internal handle limit.
- If `Transient`: same problem as Scoped, worse.

The plan doesn't specify. The likely-correct answer: `IZvecCollection<T>` is `Singleton` (one handle per collection per process), with internal locking for writes. But this isn't documented.

**Concrete fix:**
- Document DI lifetimes in `docs/architecture/di-lifetimes.md`:
  - `IZvecFactory`: Singleton (one native engine per process)
  - `IVectorStore`: Singleton (stateless façade)
  - `IVectorStoreRecordCollection<TKey, TRecord>`: Singleton (one native collection handle per (TKey, TRecord) pair per process)
  - `IRagPipeline`: Scoped (per-request, holds no native state)
  - `IChatClient` / `IEmbeddingGenerator`: per-user-configuration (may be Singleton for a single Ollama endpoint, or Scoped for per-tenant endpoints)
- Add a DI validation test: resolving `IVectorStore` twice returns the same instance; resolving `IRagPipeline` twice returns different instances.
- Add a guard in `AddZVecVectorStore`: if `IZvecFactory` is already registered with a different lifetime, throw.

**Owner:** `zvec-vectordata-expert` + `zvec-native-aot-expert`. **Effort:** 4 hours.

---

### Finding 2.4 — Vector dimensionality fixed at POCO level — embedder swap requires schema recreation

**Evidence:**
- ZVec.NET README: `[ZVecVector(768, Metric = ZVecMetricType.Cosine, M = 32, EfConstruction = 256)]` — dimensionality (768) is a POCO attribute, baked into the schema at create-time.
- ZVec.NET README: "Open loads Schema from on-disk metadata (no schema argument)" — schema is persisted; changing the POCO after creation causes mismatch.
- README quickstart (line 48): `opts.Embedder = ollama.Embeddings(model: "nomic-embed-text");` — `nomic-embed-text` is 768-d.
- Strategic plan §4.3: "Embedding: chunks → vectors (delegates to M.E.AI IEmbeddingGenerator)".

**Why it matters:** A user who starts with `nomic-embed-text` (768-d) and later switches to `text-embedding-3-small` (1536-d) cannot just change the embedder in `AddZVecRag`. The ZVec collection's schema declares 768-d; inserting 1536-d vectors throws `ArgumentException` at runtime.

The migration path is:
1. Create a new collection with 1536-d schema.
2. Re-embed all documents (requires access to original text — may not be available if only vectors were stored).
3. Switch the application to the new collection.
4. Delete the old collection.

This is a full re-ingestion, not a configuration change. The plan doesn't acknowledge this constraint, and users will discover it at runtime when switching embedders.

**Concrete fix:**
- Document the embedder-schema coupling in `docs/reference/embedder-dimensionality.md`: "The embedder's output dimensionality MUST match the ZVec collection's `[ZVecVector(dim)]` attribute. Changing embedders requires creating a new collection and re-ingesting all documents."
- Add a runtime check in `AddZVecRag`: if the embedder's dimensionality (queryable via `IEmbeddingGenerator.GetModelInfo()` or similar) doesn't match the schema's `[ZVecVector(dim)]`, throw at startup with a clear error message.
- Consider a `EmbedderMigrationTool` (post-v1) that automates re-ingestion when switching embedders.
- In the README quickstart, add a comment: `// nomic-embed-text is 768-d — must match [ZVecVector(768)] in your record schema`.

**Owner:** `zvec-vectordata-expert` + `zvec-rag-pipeline-expert`. **Effort:** 4 hours.

---

### Finding 2.5 — `Embedding<float>` to `ReadOnlyMemory<float>` pinning path and GC implications

**Evidence:**
- ZVec.NET README: "Pin-based vector pipelines: `ReadOnlyMemory<float>` on hot paths — no intermediate `float[]` copies on the query pin path."
- README quickstart: delegates embedding to `IEmbeddingGenerator<string, Embedding<float>>`.
- M.E.AI's `Embedding<float>` is a wrapper around `ReadOnlyMemory<float>` (via `.Vector` property).
- No doc specifies how `Embedding<float>.Vector` is passed to ZVec.NET's native API.

**Why it matters:** "Pin-based" means the .NET runtime pins the `float[]` underlying the `ReadOnlyMemory<float>` so the GC doesn't relocate it during the native call. There are three pinning strategies, with different GC implications:

1. **`fixed` statement on `Span<T>`** — fast, but requires the memory to be a managed array (not native memory). Pins for the duration of the `fixed` block. GC cannot relocate during this window.
2. **`GCHandle.Alloc(array, Pinned)`** — longer-lived pin, can be held across async calls. More expensive; creates GC fragmentation.
3. **`MemoryMarshal.TryGetArray` + `fixed`** — checks if the `ReadOnlyMemory<T>` is backed by a managed array; if yes, pins it; if no (e.g., native memory), uses a different path.

M.E.AI's `Embedding<float>.Vector` may be backed by:
- A managed `float[]` (most Ollama/OpenAI clients) — pinnable via `fixed`.
- A `MemoryManager<float>` (custom embedders, e.g., ONNX Runtime) — may require `TryGetArray` or a copy.
- Native memory (rare) — requires a copy to a managed array first.

If ZVec.NET's native API requires a `float*` (via `fixed`), and the embedder returns a `MemoryManager<float>`-backed `ReadOnlyMemory<float>`, the pin fails and a copy is required — defeating the "zero-copy" claim.

**Concrete fix:**
- Audit ZVec.NET's native P/Invoke signature: does it accept `float*` (requires `fixed`), `ReadOnlySpan<float>` (allows `MemoryMarshal.TryGetArray`), or `in float` array?
- Add a benchmark: embed a 768-d vector via Ollama, pass to ZVec.NET, measure allocations. If allocations > 0, the "zero-copy" claim is false for that path.
- Document the pinning strategy in `docs/architecture/vector-pinning.md`: "Embeddings backed by managed `float[]` are pinned via `fixed` (zero-copy). Embeddings backed by `MemoryManager<T>` fall back to a single copy (one allocation per query)."
- For M.E.AI embedders that use `MemoryManager<T>` (ONNX Runtime, LLamaSharp), consider contributing a PR to expose a managed-array path.

**Owner:** `zvec-performance-expert` + `zvec-native-aot-expert`. **Effort:** 1 day.

---

## 3. Native AOT & Interop

### Finding 3.1 — ZVec.NET claims "no IL warnings" not "verified runtime execution" — RAG plan escalates the claim

**Evidence:**
- ZVec.NET README: "Native AOT ready: `<IsAotCompatible>true</IsAotCompatible>` — publish AOT without IL warnings; consumer POCOs need no annotations."
- Master Spec Task 0.2.4: "Run `dotnet publish -c Release -r win-x64` on `ZVec.AotTestApp` against `ZVec.NET 1.0.0-beta.5`. Verified 100% successful execution across model resolution, document conversion, vector pinning, and POCO restoration under Native AOT."
- Master Spec Task 0.2.4 acceptance: "Native AOT binary built and executed successfully; 100% test pass."

**Why it matters:** ZVec.NET's own claim is **compile-time** (publish succeeds with no IL2026/IL3050 warnings). The RAG plan's claim is **runtime** ("100% successful execution across model resolution, document conversion, vector pinning, and POCO restoration"). These are different claims:

- Compile-time AOT success means: the trimmer didn't find reflection patterns it couldn't analyze.
- Runtime AOT success means: the published binary actually executes the operations without throwing `PlatformNotSupportedException`, `MissingMethodException` (from trimmed-away reflection), or `EntryPointNotFoundException` (from native symbol resolution).

The trimmer is conservative — it can report "no warnings" but still leave reflection-based code that fails at runtime because the trimmer couldn't see the call graph deeply enough. This is especially true for:
- `ZVecCollectionSchemaBuilder.From<T>()` (Finding 2.1) — reflection over POCO attributes.
- `System.Text.Json` serialization (used by M.E.AI for chat message serialization) — historically trim-tricky.
- `Microsoft.ML.Tokenizers` (Finding 7.3) — model file loading may use reflection.

The RAG plan's "100% successful execution" claim is stronger than ZVec.NET's own claim and is unsupported by published evidence.

**Concrete fix:**
- Align the claim with ZVec.NET's: "ZVec.NET publishes with `<IsAotCompatible>true</IsAotCompatible>` and zero IL warnings. The RAG plan's `ZVec.AotTestApp` extends this to runtime verification on win-x64."
- Publish the `ZVec.AotTestApp` test results (test names, pass/fail, execution log) as evidence — not just "100% test pass".
- Acknowledge that "publishes without warnings" ≠ "runs without exceptions" — the former is a trimmer report, the latter is a test execution report.
- For other RIDs (linux, macOS, mobile), the runtime verification is pending (per prior review Finding 2.1).

**Owner:** `zvec-native-aot-expert`. **Effort:** 2 hours (claim alignment + evidence publication).

---

### Finding 3.2 — `SafeZvecHandle` critical finalizer on iOS under NativeAOT

**Evidence:**
- ZVec.NET README: "Collection handles owned by `SafeZvecHandle` (close-only); `Dispose` closes, `Destroy` deletes then closes; `Shutdown` disposes all tracked open collections before `zvec_shutdown`."
- iOS NativeAOT constraints: finalizers run on a dedicated finalizer thread; native library must still be loaded when finalizer runs; iOS suspends apps (not fully terminates) — finalizers may run during suspension.
- Master Spec Story 0.2: AOT audit — no mention of SafeHandle finalizer semantics on iOS.

**Why it matters:** `SafeHandle`'s critical finalizer calls into native code (`zvec_close_collection`). On iOS:

1. **App suspension**: When an iOS app is backgrounded, the OS suspends the process. Finalizers may run during suspension to release memory. If the finalizer calls `zvec_close_collection` while the native library is in a suspended state (e.g., mid-operation), the native call may deadlock or corrupt state.
2. **App termination**: On termination, iOS sends `SIGTERM` then `SIGKILL` after a grace period. Finalizers must complete within the grace period. If `zvec_close_collection` is slow (e.g., flushes index to disk), the finalizer may not complete, leaving the index in an inconsistent state.
3. **NativeAOT finalizer thread**: Under NativeAOT, the finalizer thread is a dedicated thread (not a ThreadPool thread). If the native call blocks (e.g., waiting for a file lock), the entire finalizer queue stalls — no other objects can be finalized.

ZVec.NET's `Shutdown` method ("disposes all tracked open collections before `zvec_shutdown`") suggests explicit shutdown is preferred over finalizer-based cleanup. But the RAG plan's `AddZVecRag` doesn't specify when `Shutdown` is called — is it hooked to `IHostApplicationLifetime.ApplicationStopping`? If not, the app relies on finalizers, which is unsafe on iOS.

**Concrete fix:**
- Hook `IZvecFactory.Shutdown()` to `IHostApplicationLifetime.ApplicationStopping` in `AddZVec` / `AddZVecRag`. Document that explicit shutdown is required; finalizers are a safety net, not the primary path.
- Add an iOS-specific test: launch a MAUI iOS app, open a ZVec collection, background the app, foreground it, verify the collection is still usable (no corruption from suspension-time finalizers).
- Document in `docs/reference/ios-finalizer-constraints.md`: "iOS apps must call `IZvecFactory.Shutdown()` on `ApplicationStopping`. Relying on finalizers risks index corruption on app suspension."
- Consider a `SafeZvecHandle` refinement: in the critical finalizer, check a `disposed` flag and skip the native call if explicit shutdown already closed the handle.

**Owner:** `zvec-native-aot-expert`. **Effort:** 1 day.

---

### Finding 3.3 — ZVec.NET CI tests only net8.0 — net9.0/net10.0 builds untested

**Evidence:**
- ZVec.NET README: "Managed CI / `simulate-pack` run tests on **net8.0** only (LTS floor); the package still ships net8/net9/net10."
- Master Spec Story 1.1.1: "TFMs: net8.0;net9.0;net10.0".
- Strategic plan §2.3: "3 TFMs: net8.0, net9.0, net10.0 (LTS floor: .NET 8)".

**Why it matters:** The package ships three TFMs, but CI only tests net8.0. The net9.0 and net10.0 builds may have:
- Different runtime behavior (e.g., `System.Threading.Lock` in .NET 9+, changed GC behavior in .NET 10).
- Different AOT trimmer behavior (each runtime version has trimmer improvements).
- Different P/Invoke marshalling (rare, but possible for `SafeHandle` semantics).

A user on .NET 10 who hits a bug that only reproduces on net10.0 will get no support — CI never tested it. The "3 TFMs supported" claim is operationally weak: shipped ≠ tested.

**Concrete fix:**
- Expand ZVec.NET's CI matrix to test all three TFMs (at least on win-x64 and linux-x64). If this doubles CI time, use a matrix split (net8.0 full suite, net9.0/net10.0 smoke tests).
- In the RAG plan, clarify: "ZVec.NET CI tests net8.0 only; the RAG plan's CI will test all three TFMs for the RAG layer, but inherits ZVec.NET's net9.0/net10.0 gap for the underlying engine."
- Add a `docs/reference/tfm-support-matrix.md` showing which TFM is tested at which CI layer.

**Owner:** `zvec-native-aot-expert` (coordinate with ZVec.NET upstream). **Effort:** 4 hours (CI matrix expansion).

---

### Finding 3.4 — HNSW-RaBitQ/DiskANN runtime fallback semantics undefined in RAG layer

**Evidence:**
- ZVec.NET README: "HNSW-RaBitQ on ARM: Upstream ISA (x86_64 + AVX2 only); SDK throws `PlatformNotSupportedException` before native call."
- ZVec.NET README: "DiskANN on non-Linux: Upstream Linux-only (libaio optional via dlopen); same SDK gate."
- Strategic plan §2.3: "Full engine surface: HNSW, Flat, IVF, HNSW-RaBitQ (x86_64+AVX2 only), DiskANN (Linux only), Vamana, Invert, FTS indexes."
- README quickstart: `opts.StoragePath = "./rag.zvec"` — no index type selection.
- Master Spec Story 1.4: `ZVecVectorizableRecordCollection<TRecord, TKey>` — no mention of index type configuration.

**Why it matters:** The RAG plan's `AddZVecRag` doesn't expose index type selection. The connector presumably picks a default (likely HNSW for dense, FTS for keywords). But:

- On ARM (Android, iOS, linux-arm64, osx-arm64), HNSW-RaBitQ throws `PlatformNotSupportedException`. If the default is HNSW-RaBitQ (for its quantization benefits), ARM users hit an exception at schema creation.
- On non-Linux (Windows, macOS), DiskANN throws. If a user explicitly selects DiskANN on Windows, they get an exception.
- The fallback (HNSW instead of HNSW-RaBitQ) is automatic in ZVec.NET? Or does the user need to explicitly select HNSW? The ZVec.NET README says the SDK "throws before native call" — suggesting no automatic fallback.

The RAG plan doesn't document:
- What the default index type is for each platform.
- Whether the user can override via `AddZVecRag` options.
- What happens when a user selects an unsupported index type on their platform (clear error? silent fallback?).

**Concrete fix:**
- Expose `IndexType` in `ZVecRagOptions` (or `ZVecVectorStoreOptions`): `IndexType.HNSW` (default, all platforms), `IndexType.HNSWRaBitQ` (x86_64+AVX2 only), `IndexType.DiskANN` (Linux only), `IndexType.Flat`, `IndexType.IVF`, `IndexType.Vamana`.
- At `AddZVecRag` time, validate the selected index type against the current platform. Throw `PlatformNotSupportedException` with a clear message: "HNSW-RaBitQ requires x86_64+AVX2. Current platform is arm64. Use HNSW instead."
- Document the platform-index compatibility matrix in `docs/reference/index-platform-matrix.md`.
- In the README quickstart, add a comment: `// Default index: HNSW (all platforms). For x86_64+AVX2, opts.IndexType = IndexType.HNSWRaBitQ enables quantization.`.

**Owner:** `zvec-vectordata-expert` + `zvec-native-aot-expert`. **Effort:** 4 hours.

---

### Finding 3.5 — `maccatalyst-arm64` is CI-soft but counted in "9 HARD RIDs"

**Evidence:**
- ZVec.NET README: "Included in Pack from beta.3.2; CI remains **soft** until a later release promotes it to pack-required HARD: `maccatalyst-arm64` — Mac Catalyst / MAUI (`macabi`)."
- Strategic plan §2.3: "9 HARD native RIDs: win-x64, linux-x64, linux-arm64, osx-arm64, osx-x64, android-arm64, android-x64, ios-arm64, iossimulator-arm64 (+ maccatalyst-arm64 in pack, CI soft)".
- Strategic plan §12.1: "Native RIDs (9 HARD): ... (+ maccatalyst-arm64 in pack)".

**Why it matters:** The strategic plan lists "9 HARD RIDs" and then adds "(+ maccatalyst-arm64 in pack, CI soft)" — which is a 10th RID with soft CI. The "9 HARD" count is correct for the 8 fully-hard RIDs + ... wait, let me count: win-x64, linux-x64, linux-arm64, osx-arm64, osx-x64, android-arm64, android-x64, ios-arm64, iossimulator-arm64 = 9. maccatalyst is the 10th, soft.

So "9 HARD RIDs" is accurate IF maccatalyst is excluded. But the strategic plan mentions maccatalyst in the same breath, creating ambiguity. A reader may interpret "9 HARD RIDs (+ maccatalyst)" as "10 RIDs, all hard" — which is false. maccatalyst is soft (CI doesn't gate on it; failures don't block release).

For MAUI Blazor Hybrid (the flagship demo, per strategic plan §4.1), Mac Catalyst is a supported MAUI target. If a user runs the MAUI demo on macOS Catalyst and hits a ZVec native issue, they're on a soft-CI RID — the issue may be a known soft-fail that wasn't caught.

**Concrete fix:**
- Reword §2.3: "9 HARD native RIDs (CI-gated): win-x64, linux-x64, linux-arm64, osx-arm64, osx-x64, android-arm64, android-x64, ios-arm64, iossimulator-arm64. maccatalyst-arm64 ships in the NuGet pack but CI is soft (not gating)."
- Add a `docs/reference/rid-matrix.md` with columns: RID, Native file, CI status (Hard/Soft), Pack status (Yes/No), Tested in RAG plan (Yes/No).
- For MAUI Blazor Hybrid samples (Sample 03), document which RIDs are tested. If maccatalyst is not tested, mark the Mac Catalyst path as "experimental".

**Owner:** `zvec-native-aot-expert`. **Effort:** 2 hours.

---

## 4. Hybrid Search & Rerank Semantics

### Finding 4.1 — RRF `k` constant and dense/FTS weight ratio undefined

**Evidence:**
- ZVec.NET README: "Hybrid search: dense+sparse with filter + RRF rerank, dense + FTS + weighted rerank, multi-vector + RRF" and "`ZVecRrfReranker`, `ZVecWeightedReranker` — both in-DB natively".
- README quickstart (line 50): `opts.HybridSearch = true;`.
- Strategic plan §6: "Hybrid search semantics | Medium | ZVec already supports dense + FTS + RRF natively. Bridge: VectorData 'hybrid' → ZVec multi-query + `ZVecRrfReranker`. Tunable weights."
- No doc specifies: RRF `k` value (conventional 60), dense vs FTS weight ratio, top-K per retriever.

**Why it matters:** RRF (Reciprocal Rank Fusion) computes `score = Σ 1/(k + rank_i)` for each retriever `i`. The `k` constant controls the influence of top-ranked vs lower-ranked results:
- `k=60` (conventional): top ranks dominate; lower ranks contribute marginally.
- `k=1`: rank 1 contributes 0.5, rank 2 contributes 0.33 — much steeper falloff.
- `k=100`: flatter; lower ranks contribute more.

If ZVec's `ZVecRrfReranker` uses a different default `k` than the M.E.VectorData ecosystem expects (or than the user's previous solution used), retrieval quality changes silently. A user migrating from Azure AI Search (which uses its own hybrid semantics) to ZVec.Rag will see different rankings for the same query — and won't know why.

For `ZVecWeightedReranker`: the dense/FTS weight ratio (e.g., 0.7 dense + 0.3 FTS) determines whether semantic matches or keyword matches dominate. The plan says "tunable weights" but doesn't expose them (Finding 1.6).

**Concrete fix:**
- Document ZVec's default RRF `k` (read the ZVec.NET source or ZVec C++ docs). If it's not 60, document the deviation.
- Expose `RrfK` and `DenseWeight`/`FtsWeight` in `HybridSearchOptions` (see Finding 1.6).
- Add a benchmark: same query, same dataset, vary `k` from 1 to 100, measure nDCG@10. Publish the curve in `docs/reference/hybrid-search-tuning.md`.
- For migration from other vector DBs, document the RRF `k` mapping: "Azure AI Search uses k=60; ZVec default is k=60; same rankings expected."

**Owner:** `zvec-rag-pipeline-expert` + `zvec-performance-expert`. **Effort:** 1 day.

---

### Finding 4.2 — `Citation.Score` field after RRF is rank-based, not similarity

**Evidence:**
- Master Spec Story 2.3.2: "`Citation` record (`SourceDoc`, `Page`, `Offset`, `Score`, `ChunkId`)."
- Strategic plan §4.4: `opts.CitationOrder = CitationOrder.ScoreDescending;`.
- ZVec.NET README: hybrid search returns fused results via `ZVecRrfReranker`.

**Why it matters:** After RRF rerank, the `Score` field on each result is a fused rank score (`1/(k+rank)`) — not a cosine similarity (0–1) and not a BM25 score (unbounded). These scores are incomparable across retrievers and across queries:
- A score of 0.0167 (rank 1, k=60) on query A is not "more relevant" than a score of 0.0147 (rank 2, k=60) on query B.
- Filtering `Score > 0.5` after RRF returns nothing (max RRF score is `1/61 ≈ 0.0164` for a single retriever).
- Sorting by `Score` descending is correct for ranking, but the score itself is meaningless for thresholding.

If the user expects `Citation.Score` to be a cosine similarity (the intuitive interpretation for "how relevant is this chunk"), they will misuse it for filtering and thresholding. The plan's `CitationOrder.ScoreDescending` option reinforces this misinterpretation.

**Concrete fix:**
- Rename `Citation.Score` to `Citation.RankScore` (or `Citation.FusedScore`) to signal it's a fused rank score, not a similarity.
- Add `Citation.DenseScore` (cosine similarity from dense retriever) and `Citation.FtsScore` (BM25 from FTS retriever) as separate fields, populated only when hybrid search is used. These are meaningful for thresholding.
- Document in `docs/reference/citation-score-semantics.md`: "`RankScore` is for sorting (descending). `DenseScore` (0–1, cosine similarity) is for thresholding (e.g., `DenseScore > 0.7` for high-confidence matches). `FtsScore` (unbounded, BM25) is for relative ranking within FTS results."
- Add a validation test: `Assert.True(citation.DenseScore >= 0 && citation.DenseScore <= 1)` for hybrid search results.

**Owner:** `zvec-rag-pipeline-expert` + `zvec-vectordata-expert`. **Effort:** 4 hours.

---

### Finding 4.3 — `IKeywordHybridSearchable<TRecord>` contract details undefined

**Evidence:**
- Master Spec Story 1.7.2: "Implement `IKeywordHybridSearchable<TRecord>` bridge in `ZVecVectorizableRecordCollection`."
- Strategic plan §1.1: "first-party-style `Microsoft.Extensions.VectorData` connector that backs `IVectorStore`, `IVectorizedSearch<TRecord>`, and `IVectorizableRecordCollection<TRecord, TKey>` with `IZvecCollection<T>` from ZVec.NET."
- No doc specifies the method signature, return type, or how `IKeywordHybridSearchable<TRecord>` composes with `IVectorizedSearch<TRecord>`.

**Why it matters:** `IKeywordHybridSearchable<TRecord>` is a real M.E.VectorData interface for hybrid search. Its contract:
- `Task<VectorSearchResults<TRecord>> HybridSearchAsync(HybridSearchOptions<TRecord> options, ...)`.
- `HybridSearchOptions<TRecord>` has `Vector`, `VectorizableText` (mutually exclusive), `TopK`, `Filter`, `Keywords` (string collection).

The bridge must define:
- Does `VectorizableText` trigger an internal embedder call? If yes, the connector needs an `IEmbeddingGenerator` reference — but `IVectorStore` doesn't typically hold one. The bridge must either require the user to pass an embedder, or use a callback.
- How are `Keywords` mapped to ZVec's FTS? ZVec's FTS likely has its own tokenizer (different from M.ML.Tokenizers). Keyword matching semantics may differ (stemming, stop words, phrase matching).
- What `Filter` semantics apply? Pre-retrieval (ZVec native) or post-fusion?

The plan says "implement the bridge" without addressing these contract details.

**Concrete fix:**
- Document the method signature in `docs/architecture/hybrid-search-bridge.md`:
  ```csharp
  public sealed class ZVecVectorizableRecordCollection<TRecord, TKey> :
      IVectorStoreRecordCollection<TKey, TRecord>,
      IVectorizedSearch<TRecord>,
      IKeywordHybridSearchable<TRecord>
  {
      Task<VectorSearchResults<TRecord>> HybridSearchAsync(
          HybridSearchOptions<TRecord> options, CancellationToken ct = default);
  }
  ```
- For `VectorizableText`: require an `IEmbeddingGenerator<string, Embedding<float>>` in the collection's constructor. Throw if `VectorizableText` is used without an embedder.
- For `Keywords`: document the FTS mapping (ZVec's FTS tokenizer, stemming rules, phrase support). If ZVec's FTS doesn't support phrase queries, document the limitation.
- Add conformance tests for each `HybridSearchOptions<TRecord>` field.

**Owner:** `zvec-vectordata-expert`. **Effort:** 1 day.

---

### Finding 4.4 — `CitationOrder` vs `Score` sorting interaction

**Evidence:**
- Strategic plan §4.4: `opts.CitationOrder = CitationOrder.ScoreDescending;`.
- Master Spec Story 2.3.2: `Citation` has `Score` field.
- Master Spec Story 2.3.1: "Test ... citation order formatting."

**Why it matters:** `CitationOrder` is a pipeline-level option, but `Score` is per-citation. If the user sets `CitationOrder = ScoreDescending`, the pipeline sorts citations by `Score` descending before streaming them to the LLM. But:
- For hybrid search, `Score` is a fused RRF score (Finding 4.2) — sorting by it is correct for ranking.
- For dense-only search, `Score` is a cosine similarity — sorting by it is correct.
- For FTS-only search, `Score` is a BM25 score — sorting by it is correct.
- If the user switches `HybridSearch` from true to false, the `Score` semantics change, but `CitationOrder = ScoreDescending` still works — coincidentally, because all three score types sort correctly descending.

However, `CitationOrder` doesn't expose other useful orderings:
- `ChunkOrderAscending` (by `Offset` within `SourceDoc`) — for "read the document in order" use cases.
- `SourceDocThenChunkOrder` — group citations by document, then by chunk order within document.
- `PageAscending` — for "cite in page order" use cases.

The plan only defines `ScoreDescending`. The other orderings are absent.

**Concrete fix:**
- Expand `CitationOrder` enum: `ScoreDescending`, `ChunkOrderAscending`, `SourceDocThenChunkOrder`, `PageAscending`, `None` (insertion order from retriever).
- Document the default: `ScoreDescending` (preserves retriever ranking).
- Add tests for each ordering mode.
- For `PageAscending` with non-paginated documents (plain text), fall back to `ChunkOrderAscending` and log a warning.

**Owner:** `zvec-rag-pipeline-expert`. **Effort:** 4 hours.

---

## 5. Citation Schema & Persistence

### Finding 5.1 — `EnsureSchema` only adds nullable numeric columns — citation string fields must be create-time

**Evidence:**
- ZVec.NET README: "DDL note: native `add_column` / typed `EnsureSchema` only add **nullable numeric** columns. Put string/array fields in the create-time schema."
- Master Spec Story 2.3.2: "`Citation` record (`SourceDoc`, `Page`, `Offset`, `Score`, `ChunkId`)."
- Master Spec Task 2.2.4: "Ensure chunk metadata (`SourceDoc`, `Page`, `Offset`, `ChunkId`) is cleanly attached to vectors."
- Strategic plan Epic 8.4: "Schema migrations for evolving record types."

**Why it matters:** The `Citation` schema has:
- `SourceDoc` (string) — **cannot be added via `EnsureSchema`**; must be create-time.
- `Page` (int) — nullable numeric; can be added via `EnsureSchema`.
- `Offset` (long) — nullable numeric; can be added via `EnsureSchema`.
- `Score` (float) — nullable numeric; can be added via `EnsureSchema`.
- `ChunkId` (string or guid) — if string, cannot be added via `EnsureSchema`; if numeric (long), can be added.

This means:
- The citation schema MUST be declared at collection creation time, not migrated later.
- If v1.0 ships with `SourceDoc` as a string field and v1.1 needs to add a `SourceDocHash` field (also string), the only migration path is: create a new collection with the new schema, re-ingest all documents, switch the application, delete the old collection.
- "Schema migrations for evolving record types" (Epic 8.4) is impossible for string fields given ZVec.NET's DDL constraint. The plan doesn't acknowledge this.

**Concrete fix:**
- Design the v1.0 citation schema to be forward-compatible: include all plausible string fields at create-time, even if unused in v1.0 (e.g., `SourceDocHash`, `SourceUri`, `Author`, `Title`).
- For numeric fields (`Page`, `Offset`, `Score`, `ChunkIndex`), use `EnsureSchema` for additive migrations.
- Document the schema evolution constraint in `docs/reference/schema-evolution.md`: "String fields cannot be added via `EnsureSchema`. Plan all string citation fields at v1.0 collection creation. Numeric fields can be added additively."
- Update Epic 8.4 to: "Schema migrations for evolving record types — limited to nullable numeric fields. String field additions require collection recreation and re-ingestion."
- Consider a `ZVecRagSchemaV1` standard schema (with all plausible fields) that the connector uses by default, so users don't have to design their own.

**Owner:** `zvec-vectordata-expert` + `zvec-rag-pipeline-expert`. **Effort:** 1 day (schema design + doc).

---

### Finding 5.2 — `Citation.Offset` semantics undefined (byte/char/token/chunk)

**Evidence:**
- Master Spec Story 2.3.2: `Citation.Offset` field.
- No doc specifies the unit or meaning of `Offset`.

**Why it matters:** "Offset" is ambiguous. Four plausible interpretations:
1. **Byte offset in the source file** — useful for binary formats (PDF), but breaks for text (UTF-8 multi-byte chars).
2. **Character offset in the extracted text** — useful for highlighting in UI, but breaks for formats with non-linear text (PDF pages, tables).
3. **Token offset in the tokenized stream** — useful for re-embedding, but couples `Offset` to the tokenizer (Finding 7.1).
4. **Chunk index within the source document** — useful for "chunk 3 of 12", but loses positional precision.

Each choice has downstream implications:
- For PDF citation highlighting (UI feature), byte/character offset is needed.
- For "previous chunk / next chunk" navigation, chunk index is needed.
- For re-embedding after edits, token offset is needed.

The plan doesn't specify, so the implementer will pick one (likely `ChunkIndex`, the easiest) — and users who need character offset for UI highlighting will be stuck.

**Concrete fix:**
- Define `Offset` as `long` character offset in the extracted text (most useful for UI highlighting).
- Add `ChunkIndex` (int) as a separate field for chunk-within-document navigation.
- For PDF, document that `Offset` is relative to the extracted text (not the PDF byte stream), and `Page` is the PDF page number.
- Add a `Highlight(extractedText, offset, length)` helper method that returns a snippet around the offset for UI display.

**Owner:** `zvec-rag-pipeline-expert`. **Effort:** 4 hours.

---

### Finding 5.3 — `Citation.Page` nullability for non-paginated documents

**Evidence:**
- Master Spec Story 2.3.2: `Citation.Page` field (type not specified).
- README line 14: "precise document & page citation tracking".
- No doc specifies the type (`int`, `int?`), the default for non-paginated documents (plain text, Markdown), or the semantics.

**Why it matters:** If `Page` is `int` (non-nullable):
- For plain text/Markdown (no pages), the field must be set to a sentinel value (`0`, `-1`). Filtering `Page == 0` returns all non-paginated citations — may be useful or may be a footgun.
- ZVec's filter translator must handle the sentinel correctly (e.g., `Page > 0` to exclude non-paginated).

If `Page` is `int?` (nullable):
- For non-paginated documents, `Page = null`. Filtering `Page == null` requires ZVec to support null comparisons in filters — the plan's filter translator (Story 1.5) doesn't list null handling as a supported operator.
- Nullable int in ZVec's schema: `EnsureSchema` supports "nullable numeric" — so `int?` is OK. But filtering on null requires `IS NULL` / `IS NOT NULL` operators not in the `ZVecFilterOperators` enum (Story 1.2.2).

Either choice has implications. The plan doesn't make the choice.

**Concrete fix:**
- Use `int?` (nullable) for `Page`. Non-paginated documents set `Page = null`.
- Add `IsNull` / `IsNotNull` to `ZVecFilterOperators` enum and to the filter translator.
- Document: "Plain text and Markdown documents have `Page = null`. PDF and DOCX have `Page` set to the 1-indexed page number."
- Add a test: ingest a plain text document, retrieve, assert `citation.Page == null`.

**Owner:** `zvec-vectordata-expert` + `zvec-rag-pipeline-expert`. **Effort:** 4 hours.

---

### Finding 5.4 — `Citation.SourceDoc` type and path semantics

**Evidence:**
- Master Spec Story 2.3.2: `Citation.SourceDoc` field (type not specified).
- README line 14: "precise document & page citation tracking".
- No doc specifies: is `SourceDoc` a file path, a URI, a document ID (GUID), or a content hash?

**Why it matters:** Each choice has different implications:

1. **File path** (`/docs/policy.pdf`):
   - Pro: Human-readable, directly usable for "open document" UI feature.
   - Con: Path changes when files move; security risk (path traversal if user-controlled); not portable across machines.

2. **URI** (`file:///docs/policy.pdf` or `https://example.com/docs/policy.pdf`):
   - Pro: Standardized, supports remote documents.
   - Con: Requires URI parsing for display; `file://` URIs have cross-platform issues.

3. **Document ID (GUID)**:
   - Pro: Stable across moves; no security risk; portable.
   - Con: Not human-readable; requires a sidecar mapping (GUID → path) for UI.

4. **Content hash (SHA-256)**:
   - Pro: Deduplication (same content = same hash); integrity check.
   - Con: Not human-readable; changes when content is edited (even typo fixes).

The plan doesn't specify. The likely default is file path (easiest for the implementer), which has security and portability issues.

**Concrete fix:**
- Use `string` for `SourceDoc` and define it as a **document ID** (user-provided or auto-generated GUID). Decouple from file path.
- Add a separate `SourceUri` field (string, optional) for the original file path or URL. This is for UI display, not filtering.
- Add a `SourceHash` field (string, optional) for content deduplication.
- Document in `docs/reference/citation-schema.md`: "`SourceDoc` is a stable document identifier (GUID recommended). `SourceUri` is the original location (file path or URL) for display. `SourceHash` is the SHA-256 of the extracted text for deduplication."
- Add a `DocumentRegistry` sidecar (ZVec collection or SQLite table) mapping `SourceDoc` → metadata (original path, ingestion timestamp, content hash).

**Owner:** `zvec-rag-pipeline-expert` + `zvec-vectordata-expert`. **Effort:** 1 day.

---

### Finding 5.5 — `Citation.ChunkId` type and collision semantics

**Evidence:**
- Master Spec Story 2.3.2: `Citation.ChunkId` field (type not specified).
- No doc specifies: GUID, sequential long, content hash, or composite key.

**Why it matters:** `ChunkId` is used to round-trip citations through embedding/retrieval/generation (strategic plan Epic 2.9). The type choice affects:

1. **GUID** (`Guid` or `string`):
   - Pro: Globally unique, no collision.
   - Con: 16 bytes (or 36 chars as string) — larger storage; not sortable; not human-readable.

2. **Sequential long**:
   - Pro: Compact (8 bytes); sortable; fast indexing.
   - Con: Requires a central counter (single-writer bottleneck); not portable across collections.

3. **Content hash (SHA-256 of chunk text)**:
   - Pro: Deduplication (same chunk = same ID); integrity check.
   - Con: Changes when chunk text changes (even whitespace); 32 bytes.

4. **Composite key** (`{SourceDoc}:{ChunkIndex}` as string):
   - Pro: Human-readable; debuggable.
   - Con: Larger storage; requires parsing; `SourceDoc` changes propagate.

The plan doesn't specify. ZVec.NET's `Id` convention (from the README: "public `string Id` / `ID`, **or** `[ZVecId]`") suggests `string` is the native type. But `string` ChunkId of what form?

**Concrete fix:**
- Use `string` for `ChunkId`, formatted as `{SourceDoc}:{ChunkIndex:D6}` (e.g., `doc-abc:000042`). Human-readable, debuggable, sortable.
- Add a `ChunkHash` field (string, SHA-256 of chunk text) for deduplication.
- Document the format in `docs/reference/citation-schema.md`.
- Add a validation test: `Assert.Matches(@"^[a-zA-Z0-9_-]+:\d{6}$", citation.ChunkId)`.

**Owner:** `zvec-rag-pipeline-expert`. **Effort:** 4 hours.

---

## 6. Streaming & Concurrency

### Finding 6.1 — SSE WriteAsync buffering — quickstart demonstrates wrong pattern

**Evidence:**
- README quickstart (lines 62–67):
  ```csharp
  await foreach (var chunk in rag.AskAsync(question, streamCitations: true, ct))
  {
      await Response.WriteAsync(chunk.Text, ct);
  }
  ```
- Master Spec Story 2.3.3: "Implement ASP.NET Core SSE endpoint helper `app.MapRagSseEndpoint(...)`."

**Why it matters:** `Response.WriteAsync` writes to the response body's buffer. ASP.NET Core's response body is buffered by default (`HttpContext.Response.BodyWriter`). Without an explicit `FlushAsync`, the buffer holds chunks until it fills or the response ends — defeating the purpose of streaming.

For true SSE (Server-Sent Events) streaming:
- Each chunk must be flushed immediately so the browser's `EventSource` receives it.
- The HTTP response must have `Content-Type: text/event-stream`, `Cache-Control: no-cache`, `Connection: keep-alive`.
- Each SSE message must be formatted as `data: {payload}\n\n`.

The quickstart demonstrates none of this. A user copying the quickstart will see "no streaming" — the entire response arrives at once at the end. The `MapRagSseEndpoint` helper (Story 2.3.3) presumably handles this correctly, but the quickstart bypasses it with raw `WriteAsync`.

**Concrete fix:**
- Update the README quickstart to use `MapRagSseEndpoint`:
  ```csharp
  app.MapRagSseEndpoint("/chat", async (string question, IRagPipeline rag, CancellationToken ct) =>
      rag.AskAsync(question, streamCitations: true, ct));
  ```
- If raw `WriteAsync` is shown for educational purposes, add a comment: `// NOTE: For real SSE streaming, use app.MapRagSseEndpoint(...). This snippet buffers; it does not stream.`.
- Add a `docs/guides/sse-streaming.md` showing the correct SSE setup with `Response.BodyWriter.WriteAsync` + `FlushAsync`.
- Add an integration test that verifies SSE chunks arrive incrementally (not buffered) using `HttpClient` with `ReadAsStreamAsync`.

**Owner:** `zvec-rag-pipeline-expert`. **Effort:** 4 hours.

---

### Finding 6.2 — `AskAsync` cancellation and sync P/Invoke

**Evidence:**
- README quickstart (line 62): `rag.AskAsync(question, streamCitations: true, ct)` — `CancellationToken` passed.
- ZVec.NET README: "async `ValueTask` for ASP.NET Core (cooperative cancel; no thread-pool offload today)".
- Strategic plan §6: "Async = wrapper over sync P/Invoke | Medium | ZVec.NET's async is 'cooperative-cancel wrapper, not thread-pool offload' by explicit design."

**Why it matters:** "Cooperative cancel" means ZVec.NET's async methods check the `CancellationToken` *between* native calls, not *during* a native call. A single `Query` P/Invoke into the native engine cannot be cancelled — it runs to completion.

For `AskAsync`, the pipeline calls:
1. Embedder (network call to Ollama — cancellable via HTTP).
2. ZVec search (sync P/Invoke — NOT cancellable mid-call).
3. LLM streaming (network call — cancellable via HTTP).

If the user cancels during step 2 (ZVec search), the cancellation doesn't take effect until step 2 completes. For a 10k-vector collection, step 2 is ~4ms (per ZVec benchmarks) — acceptable. For a 1M-vector collection with complex filter, step 2 could be 100ms+ — the user perceives a lag.

The plan doesn't document this contract. A user who cancels and sees a 100ms delay will file a bug.

**Concrete fix:**
- Document the cancellation contract in `docs/reference/cancellation.md`: "Cancellation is cooperative. `AskAsync` checks the token between pipeline stages (embed → search → generate). Native ZVec search calls cannot be cancelled mid-call; cancellation takes effect after the current native call completes."
- Add a `CancellationToken` check after the embed call and before the search call, and another after the search call and before the LLM call. This minimizes the cancellation lag to the duration of one native call.
- For long-running native calls (e.g., ingestion of 1000 chunks), document that cancellation between chunks is supported (each chunk's upsert is a separate native call).
- Add a test: cancel `AskAsync` during ZVec search, verify the cancellation exception is thrown within `2 * (native call duration)`.

**Owner:** `zvec-rag-pipeline-expert` + `zvec-performance-expert`. **Effort:** 4 hours.

---

### Finding 6.3 — `RagChunk.IsFinal` and `Usage` population timing undefined

**Evidence:**
- Master Spec Story 2.3.2: "`RagChunk` record (`Text`, `Citations`, `IsFinal`, `Usage`)."
- No doc specifies: when is `IsFinal = true`? Is there a sentinel chunk with empty text? When is `Usage` populated?

**Why it matters:** Three plausible patterns for the final chunk:

1. **Last text-bearing chunk has `IsFinal = true`**: Consumer must inspect every chunk to know when the stream ends. `Usage` is populated on this chunk.
2. **Sentinel chunk after the last text chunk**: `Text = ""`, `IsFinal = true`, `Usage` populated. Consumer handles an empty-text chunk.
3. **`IsFinal` is always `false` until the stream completes, then a final chunk with `IsFinal = true` and full `Usage`**: Similar to pattern 2.

Without specification, consumers will write brittle code:
```csharp
await foreach (var chunk in rag.AskAsync(...)) {
    if (chunk.IsFinal) {
        totalUsage = chunk.Usage; // may be null if pattern 1 and this chunk has no Usage
    } else {
        await Response.WriteAsync(chunk.Text);
    }
}
```

If `Usage` is on every chunk (cumulative) vs only on the final chunk, the consumer code differs.

**Concrete fix:**
- Specify the contract in `docs/reference/streaming-contract.md`:
  - `IsFinal = false` for all text-bearing chunks.
  - After the last text chunk, a sentinel chunk is emitted: `Text = ""`, `IsFinal = true`, `Usage` populated with cumulative token usage.
  - `Citations` are populated on the first chunk (after retrieval, before generation). Subsequent chunks have `Citations = null` or empty.
- Provide a helper extension method: `await foreach (var chunk in rag.AskAsync(...).WithTextOnly())` that filters out the sentinel and citation-only chunks.
- Add tests verifying the contract: stream produces N text chunks + 1 final sentinel; `Usage` is non-null only on sentinel; `Citations` is non-null only on first chunk.

**Owner:** `zvec-rag-pipeline-expert`. **Effort:** 4 hours.

---

### Finding 6.4 — `StoragePath` multi-instance file locking

**Evidence:**
- README quickstart (line 47): `opts.StoragePath = "./rag.zvec"`.
- ZVec.NET README: `factory.CreateAndOpen(path, schema)`, `factory.Open(path)`, `factory.OpenOrCreate(path)` — file-based storage.
- No doc specifies: what happens when two processes open the same `./rag.zvec`?

**Why it matters:** ZVec.NET uses a file-based embedded storage (analogous to SQLite). SQLite handles concurrent access via file locking (WAL mode, reader/writer locks). ZVec.NET's locking semantics are undocumented:

1. **Exclusive lock (single-writer)**: Second process fails to open with "file in use" exception.
2. **Shared lock (multi-reader, single-writer)**: Multiple processes can read; only one can write.
3. **No lock (last-writer-wins)**: Concurrent writes corrupt the file.

For an ASP.NET Core app behind a load balancer with 3 instances, all 3 instances point at `./rag.zvec`. If ZVec.NET doesn't support multi-process access, only one instance can write — the others fail or corrupt.

The plan's "single-node scale ceiling" (strategic plan §6) suggests ZVec.NET is single-process — but doesn't explicitly say "single-process exclusive access required".

**Concrete fix:**
- Document ZVec.NET's file locking semantics in `docs/reference/concurrent-access.md`: "ZVec.NET uses exclusive file access. Only one process can open a collection for writing at a time. Multiple processes reading the same collection is not supported."
- For multi-instance ASP.NET Core deployments, document the workaround: "Each app instance must use a separate `StoragePath`. For shared state, use a single-writer service (e.g., a worker that owns the ZVec collection) and expose it via HTTP/gRPC to other instances."
- Add a startup check in `AddZVecRag`: if the `StoragePath` is already locked by another process, throw a clear exception with remediation guidance.
- For MAUI (single-process by definition), no issue — but document that the app must not open the same collection from multiple threads without coordination.

**Owner:** `zvec-native-aot-expert` + `zvec-rag-pipeline-expert`. **Effort:** 4 hours.

---

### Finding 6.5 — `MaxConcurrentNativeCalls` vs `Channels` bounded capacity coupling

**Evidence:**
- Strategic plan §6: "Concurrency during ingestion | Medium | ZVec.NET already exposes `MaxConcurrentNativeCalls` / `MaxConcurrentReads` throttles. RAG pipeline uses `System.Threading.Channels` for backpressure."
- ZVec.NET README: `MaxConcurrentNativeCalls` / `MaxConcurrentReads` throttles.
- No doc specifies the relationship between `Channels` capacity and `MaxConcurrentNativeCalls`.

**Why it matters:** The ingestion pipeline has stages:
1. **Read document** (I/O-bound, no native calls) — unbounded parallelism.
2. **Chunk** (CPU-bound, no native calls) — parallelism = CPU cores.
3. **Embed** (I/O-bound, network to Ollama) — parallelism = Ollama's concurrent request limit (typically 4–10).
4. **Upsert** (sync P/Invoke, CPU-bound) — parallelism = `MaxConcurrentNativeCalls` (typically 4–8).

If `Channels` capacity between stage 3 (embed) and stage 4 (upsert) is unbounded:
- Embed stage produces embeddings faster than upsert can consume.
- Embeddings queue in the channel, consuming memory.
- For a 10k-chunk document at 768-d float (3KB per embedding), the queue can grow to 30MB — acceptable.
- For a 100k-chunk document, 300MB — problematic.

If `Channels` capacity is too small (e.g., 1):
- Embed stage blocks after every embedding, waiting for upsert.
- Embed stage's parallelism collapses to 1 — wasting Ollama's concurrent request capacity.
- Ingestion time = (embed time per chunk) × (chunk count) — 10× slower than parallel embed.

The right capacity is `MaxConcurrentNativeCalls * 2` (one batch in flight, one batch queued). But the plan doesn't specify this.

**Concrete fix:**
- Set the default `Channels` capacity between embed and upsert stages to `MaxConcurrentNativeCalls * 2`.
- Expose `IngestionOptions.ChannelCapacity` for tuning.
- Add a benchmark: 1000-chunk ingestion with `ChannelCapacity = 1` vs `MaxConcurrentNativeCalls * 2` vs `unbounded`. Publish the time and memory results.
- Document the backpressure contract: "The channel between embed and upsert stages is bounded at `2 * MaxConcurrentNativeCalls`. If upsert is slower than embed, the embed stage blocks, preventing memory blowup."

**Owner:** `zvec-performance-expert` + `zvec-rag-pipeline-expert`. **Effort:** 4 hours.

---

## 7. Tokenizer & Embedding Pipeline

### Finding 7.1 — Tokenizer-embedder coupling — chunk boundaries vs embedding boundaries

**Evidence:**
- README "Tokenizer Strategy" section: "Primary Tokenizer (`Microsoft.ML.Tokenizers`): Uses Microsoft's official high-performance, zero-allocation tokenizer engine. Supports Tiktoken BPE (OpenAI `cl100k`/`o200k`), SentencePiece (LLaMA 3, Nomic Embed), and WordPiece (BERT, MiniLM)."
- README quickstart (line 48): `opts.Embedder = ollama.Embeddings(model: "nomic-embed-text");`.
- Master Spec Task 2.2.3: "Integrate universal tokenization via `Microsoft.ML.Tokenizers` (Tiktoken BPE, SentencePiece, WordPiece) with pluggable BPE adapter for `tryAGI/Tiktoken`."

**Why it matters:** The chunker uses a tokenizer to split text into chunks of N tokens. The embedder uses its own tokenizer to convert each chunk into an embedding. If the chunker's tokenizer differs from the embedder's tokenizer:

1. **Chunk size mismatch**: The chunker counts 512 tokens (using Tiktoken cl100k), but the embedder (using SentencePiece for `nomic-embed-text`) counts 580 tokens for the same text. The chunk exceeds the embedder's max input length, causing truncation or error.
2. **Boundary misalignment**: The chunker splits at token boundary X (per Tiktoken), but the embedder's tokenizer (SentencePiece) doesn't recognize X as a boundary — it merges tokens across the chunk boundary, causing the first/last tokens of adjacent chunks to be embedded differently than they would be in continuous text.
3. **Semantic drift**: The chunk's first/last tokens are partial words (per the embedder's tokenizer), producing lower-quality embeddings for chunk edges.

The plan treats the tokenizer as a pluggable chunker, but doesn't enforce (or even document) that the chunker's tokenizer must match the embedder's tokenizer. A user who sets `Embedder = ollama.Embeddings("nomic-embed-text")` (SentencePiece) but leaves the chunker at default Tiktoken cl100k will get silently degraded embeddings.

**Concrete fix:**
- Auto-detect the embedder's tokenizer from the model name:
  - `nomic-embed-text` → SentencePiece (LLaMA 3 tokenizer).
  - `text-embedding-3-small` / `text-embedding-ada-002` → Tiktoken cl100k.
  - `all-MiniLM-L6-v2` → WordPiece (BERT tokenizer).
- Set the chunker's tokenizer to match by default. Log a warning if the user overrides the chunker tokenizer to a mismatched one.
- Add a validation check at `AddZVecRag` time: if the embedder's tokenizer (queried via `IEmbeddingGenerator.GetModelInfo()` or hardcoded mapping) doesn't match the chunker's tokenizer, throw or warn.
- Document in `docs/reference/tokenizer-embedder-coupling.md`: "The chunker's tokenizer MUST match the embedder's tokenizer. Mismatched tokenizers cause chunk-size miscount and boundary misalignment, degrading retrieval quality by 10–30% (per public RAG benchmarks)."

**Owner:** `zvec-rag-pipeline-expert`. **Effort:** 1 day.

---

### Finding 7.2 — Tokenizer model file distribution for air-gapped scenarios

**Evidence:**
- README "Tokenizer Strategy" section: `Microsoft.ML.Tokenizers` supports Tiktoken BPE, SentencePiece, WordPiece.
- `Microsoft.ML.Tokenizers` requires model files: `tiktoken_cl100k_base.tiktoken` (~1.5MB), `llama3.tokenizer.model` (~5MB), `bert-base-uncased-vocab.txt` (~230KB).
- Strategic plan Epic 5.4: "04-airgapped-enterprise-rag — AspNet + LLamaSharp + ZVec (zero network calls)".
- No doc specifies how tokenizer model files are distributed for air-gapped scenarios.

**Why it matters:** `Microsoft.ML.Tokenizers` typically downloads model files on first use (from a CDN or Hugging Face). For air-gapped scenarios (no internet), this fails at runtime.

Options:
1. **Embed model files in the NuGet package** — bloats package size by ~7MB (all three tokenizers). Acceptable for desktop; problematic for mobile.
2. **Require user to download separately** — breaks air-gapped; user must manually copy files.
3. **Use a default tokenizer that doesn't need a model file** — Tiktoken BPE with a hardcoded vocab (no file). Only works for OpenAI models; doesn't help for LLaMA 3 / BERT.
4. **Bundle per-recipe** — `ZVec.Rag.LLamaSharp` bundles LLaMA 3 tokenizer; `ZVec.Rag.ONNX` bundles BERT tokenizer. Modularity, but each recipe package is larger.

The plan doesn't pick. The air-gapped enterprise RAG sample (Epic 5.4) will fail at runtime if the tokenizer model file isn't available.

**Concrete fix:**
- Bundle the most common tokenizer model files in `ZVec.Rag` as embedded resources: `tiktoken_cl100k_base.tiktoken`, `tiktoken_o200k_base.tiktoken`, `llama3.tokenizer.model`, `bert-base-uncased-vocab.txt`. Total ~10MB — acceptable for a batteries-included package.
- Document the bundled tokenizers in `docs/reference/tokenizer-bundled-files.md`.
- For mobile (MAUI), consider a `ZVec.Rag.Mobile` lightweight package with only the most common tokenizer (Tiktoken cl100k) to reduce app size.
- Add a startup check: if the configured tokenizer's model file is missing (not bundled, not downloadable), throw a clear exception with remediation guidance.

**Owner:** `zvec-rag-pipeline-expert` + `zvec-native-aot-expert`. **Effort:** 1 day.

---

### Finding 7.3 — `Microsoft.ML.Tokenizers` SentencePiece AOT profile

**Evidence:**
- README "Tokenizer Strategy": "SentencePiece (LLaMA 3, Nomic Embed)".
- `Microsoft.ML.Tokenizers` SentencePiece implementation loads `.model` files (protobuf) via `SentencePieceBpeModel`.
- Master Spec Verification Matrix: "Native AOT & Trim | Static & Publish Audit | `PublishAot=true` CI Job | 0 warnings (`IL2026`, `IL3050`)".
- No doc verifies SentencePiece's AOT profile specifically.

**Why it matters:** SentencePiece's `.model` file is a protobuf. Protobuf deserialization in .NET has historically used `Google.Protobuf` reflection, which has trim warnings. `Microsoft.ML.Tokenizers` may use its own protobuf parser (AOT-clean) or depend on `Google.Protobuf` (trim-tricky).

If SentencePiece's loading path uses reflection, the AOT publish will emit `IL2026` warnings — violating the Verification Matrix's "0 warnings" requirement. The plan either doesn't know, or knows and doesn't document.

Tiktoken BPE (using `.tiktoken` text files, not protobuf) is likely AOT-clean. WordPiece (using `.txt` vocab files) is likely AOT-clean. SentencePiece is the suspect.

**Concrete fix:**
- Run `dotnet publish -c Release -r win-x64 /p:PublishAot=true` on a minimal app that loads each tokenizer:
  - Tiktoken cl100k — verify 0 warnings.
  - SentencePiece LLaMA 3 — verify 0 warnings (or document the warnings).
  - WordPiece BERT — verify 0 warnings.
- If SentencePiece emits warnings, either: (a) pin to a clean version, (b) fork/wrap the SentencePiece loader to avoid reflection, or (c) document SentencePiece as "not AOT-clean; use Tiktoken or WordPiece for AOT scenarios".
- Publish the AOT profile per tokenizer in `docs/reference/tokenizer-aot-matrix.md`.

**Owner:** `zvec-native-aot-expert`. **Effort:** 4 hours.

---

## 8. Local LLM Recipes

### Finding 8.1 — LLamaSharp AOT and native binary distribution

**Evidence:**
- README package table: "`ZVec.Rag.LLamaSharp` | `1.1.0-alpha` | Recipe adapter for air-gapped local LLM execution via LLamaSharp (GGUF)".
- Master Spec Story 4.1.1: "Build `ZVec.Rag.LLamaSharp` adapter implementing `IChatClient` / `IEmbeddingGenerator` over LLamaSharp."
- LLamaSharp is a P/Invoke wrapper around `llama.cpp` — separate native binary from ZVec's.
- No doc addresses: LLamaSharp's AOT profile, native binary distribution (CPU/CUDA/Metal/Vulkan backends), model file size, mobile support.

**Why it matters:** LLamaSharp bundles `llama.cpp` native binaries for multiple backends:
- CPU (all platforms) — ~50MB per RID.
- CUDA (Windows/Linux) — additional ~500MB CUDA runtime.
- Metal (macOS) — uses system Metal framework.
- Vulkan (cross-platform) — additional ~100MB Vulkan runtime.

Each backend is a separate native binary. The user must select the right backend at runtime. For `ZVec.Rag.LLamaSharp`:
- Does the package bundle all backends? (Package size: ~1GB — unacceptable.)
- Does it bundle CPU-only? (Users wanting GPU must manually install backend.)
- Does it document backend selection?

LLamaSharp's AOT profile: `llama.cpp` uses dynamic library loading for some backends (Vulkan, CUDA). Under NativeAOT, dynamic library loading is restricted (especially on iOS). The plan doesn't verify.

Model file size: GGUF models are 4–70GB. For "air-gapped enterprise RAG" (desktop/server), this is fine. For mobile, LLamaSharp is impractical (model size alone exceeds mobile storage).

**Concrete fix:**
- Document `ZVec.Rag.LLamaSharp` as **desktop-only** (Windows, Linux, macOS). Not supported on Android/iOS.
- Bundle CPU-only backend by default. Document how to switch to CUDA/Metal/Vulkan backend (install LLamaSharp's backend package separately).
- Run AOT verification on `ZVec.Rag.LLamaSharp` with CPU backend. If CUDA/Vulkan backends emit AOT warnings, document them as "GPU backends not AOT-clean; use CPU backend for AOT scenarios".
- Add a `docs/reference/llamasharp-aot.md` page with the AOT profile per backend.
- In the README package table, mark `ZVec.Rag.LLamaSharp` as "Desktop only (Windows/Linux/macOS). Not for mobile.".

**Owner:** `zvec-rag-pipeline-expert` + `zvec-native-aot-expert`. **Effort:** 1 day.

---

### Finding 8.2 — ONNX Runtime AOT and model file size

**Evidence:**
- README package table: "`ZVec.Rag.ONNX` | `1.1.0-alpha` | Recipe adapter for local ONNX embeddings (CLIP, MiniLM, Nomic, EmbeddingGemma)".
- Master Spec Story 4.1.2: "Build `ZVec.Rag.ONNX` adapter implementing `OnnxEmbedder` for CLIP, MiniLM, and EmbeddingGemma."
- ONNX Runtime native binaries are separate from ZVec's.
- Model file sizes: CLIP (~600MB), MiniLM (~90MB), Nomic (~270MB), EmbeddingGemma (~300MB).
- No doc addresses: ONNX Runtime AOT profile, model file distribution, GPU backend selection, mobile support.

**Why it matters:** `Microsoft.ML.OnnxRuntime` has historically had trim warnings (uses reflection for model loading, custom op registration). The plan doesn't verify AOT cleanliness.

ONNX Runtime backends:
- CPU (all platforms) — ~30MB per RID.
- CUDA (Windows/Linux) — additional ~400MB.
- DirectML (Windows) — additional ~50MB.
- CoreML (macOS/iOS) — uses system framework.
- NNAPI (Android) — uses system framework.

For mobile (MAUI), ONNX Runtime with CoreML/NNAPI execution provider is the path — but model file size (90MB for MiniLM) is still problematic for mobile app distribution.

For "multimodal RAG" (Sample 05, CLIP ONNX): the CLIP model is ~600MB. For desktop, fine. For mobile, unacceptable. The plan doesn't address model file distribution.

**Concrete fix:**
- Run AOT verification on `ZVec.Rag.ONNX` with CPU execution provider. If DirectML/CUDA providers emit warnings, document them.
- Document model file distribution: "ONNX models are not bundled. The user must download the model (e.g., from Hugging Face) and configure the path in `AddZVecRag(opts => opts.Embedder = new OnnxEmbedder(modelPath))`."
- For air-gapped scenarios, document how to pre-download models and bundle them with the app.
- For mobile, mark CLIP as "desktop only". MiniLM (90MB) is borderline acceptable for mobile; document the app size impact.
- Add a `docs/reference/onnx-aot.md` page with the AOT profile per execution provider.

**Owner:** `zvec-rag-pipeline-expert` + `zvec-native-aot-expert`. **Effort:** 1 day.

---

### Finding 8.3 — Multimodal RAG image preprocessing pipeline absent

**Evidence:**
- Strategic plan Epic 5.5: "05-multimodal-rag — CLIP ONNX + ZVec (lift from `demos/01-clip-onnx` Flickr8k pattern)".
- README package table: `ZVec.Rag.ONNX` covers "CLIP, MiniLM, Nomic, EmbeddingGemma".
- No doc addresses: image preprocessing (resize, normalize, channel order), image tokenizer, image embedding pipeline.

**Why it matters:** CLIP is a multimodal model — it embeds both text and images into the same vector space. To use CLIP for RAG:
1. **Image ingestion**: Load image → preprocess (resize to 224×224, normalize with CLIP's mean/std, convert to NCHW tensor) → run through CLIP image encoder → get image embedding → store in ZVec.
2. **Text query**: Embed query text via CLIP text encoder → search ZVec for nearest image embeddings → return images as citations.

The image preprocessing pipeline (steps 1–2) is non-trivial:
- Image loading: `System.Drawing` (Windows-only), `SkiaSharp` (cross-platform, separate NuGet), `ImageSharp` (cross-platform, separate NuGet).
- Preprocessing: resize, normalize, tensor conversion — each library has different APIs.
- AOT profile: `SkiaSharp` uses native bindings (libskia); `ImageSharp` is pure C# (more AOT-friendly but slower).

The plan says "lift from `demos/01-clip-onnx`" — but the demo likely uses one specific image library. The plan doesn't specify which, doesn't address AOT, and doesn't address mobile (SkiaSharp works on mobile; ImageSharp may not).

**Concrete fix:**
- Choose an image preprocessing library: `SixLabors.ImageSharp` (pure C#, AOT-friendly, cross-platform) is the recommended default.
- Add `ZVec.Rag.ImageSharp` (or include in `ZVec.Rag.ONNX`) an `ImagePreprocessor` class that handles CLIP's preprocessing.
- Document the image ingestion pipeline in `docs/guides/multimodal-rag.md`.
- Run AOT verification on the image preprocessing pipeline.
- For mobile, mark multimodal RAG as "desktop only" (CLIP model size + image preprocessing complexity).

**Owner:** `zvec-rag-pipeline-expert`. **Effort:** 2 days.

---

## 9. Testing Strategy

### Finding 9.1 — `DeterministicEmbedder` hash collision and similarity structure

**Evidence:**
- Master Spec Story 2.4.1: "Unit test `DeterministicEmbedder` (hash-based predictable vectors) and `FakeChatClient`."
- Master Spec Verification Matrix: "RAG Pipeline | Deterministic / Snapshot | Verify.Xunit + Fakes | <100ms CI execution".
- No doc specifies: vector dimensionality, hash algorithm, locality-sensitive vs random hashing.

**Why it matters:** A "hash-based predictable embedder" maps text → fixed vector via hashing. Two design choices have opposite testing implications:

1. **Random hash (e.g., MD5 of text → seed → random vector)**:
   - No similarity structure: two semantically similar texts get unrelated vectors.
   - Retrieval tests can only validate structural correctness (top-K returns N results), not semantic correctness (top result is most similar).
   - Hash collisions are rare (MD5) but possible — two texts map to the same vector.

2. **Locality-sensitive hash (e.g., SimHash, MinHash)**:
   - Similar texts get similar vectors (cosine similarity correlates with text similarity).
   - Retrieval tests can validate semantic correctness: "query 'cat' returns 'feline' before 'automobile'".
   - More complex to implement; may not be truly deterministic across runs (depends on LSH parameters).

The plan doesn't specify which. If random hash, retrieval tests are weak (can't catch semantic regressions). If LSH, the implementation is non-trivial and the plan underestimates the effort.

**Concrete fix:**
- Specify `DeterministicEmbedder` as random hash (deterministic, no similarity structure). Document: "This embedder is for pipeline testing only. It does not validate semantic retrieval quality. For semantic tests, use integration tests with a real embedder (Ollama nomic-embed-text)."
- Add a separate `SemanticTestEmbedder` (LSH-based or small pretrained model) for tests that need similarity structure. Document its limitations.
- For integration tests (real Ollama), add `[Trait("Category", "RequiresOllama")]` and gate on env var `OLLAMA_BASE_URL`. Run in CI if env var is set; skip otherwise.
- Add a `docs/reference/testing-strategy.md` page distinguishing unit tests (deterministic fakes) from integration tests (real backends).

**Owner:** `zvec-code-reviewer-expert` + `zvec-rag-pipeline-expert`. **Effort:** 4 hours.

---

### Finding 9.2 — `FakeChatClient` streaming path coverage

**Evidence:**
- Master Spec Story 2.4.1: "Unit test `DeterministicEmbedder` (hash-based predictable vectors) and `FakeChatClient`."
- M.E.AI's `IChatClient` has both `GetResponseAsync` (non-streaming) and `GetStreamingResponseAsync` (returns `IAsyncEnumerable<ChatResponseStream>`).
- No doc specifies whether `FakeChatClient` implements both paths.

**Why it matters:** `IRagPipeline.AskAsync` uses streaming (`IAsyncEnumerable<RagChunk>`). Internally, it calls `IChatClient.GetStreamingResponseAsync`. If `FakeChatClient` only implements `GetResponseAsync` (non-streaming) and throws `NotImplementedException` on `GetStreamingResponseAsync`, then:
- Unit tests using `FakeChatClient` cannot test the streaming path.
- The streaming consumer pattern (`await foreach`) is untested in CI.
- Streaming-specific bugs (e.g., cancellation mid-stream, partial chunk handling) are not caught.

The plan's "Zero Dummy Test Enforcement" (Rule 1) is in tension with a `FakeChatClient` that only implements one path.

**Concrete fix:**
- `FakeChatClient` must implement both `GetResponseAsync` and `GetStreamingResponseAsync`.
- For streaming: `GetStreamingResponseAsync` yields `ChatResponseStream` chunks from a configurable list (e.g., `FakeChatClient.WithStreamingResponses(new[] { "Hello", " world" })`).
- Add tests verifying the streaming path: `await foreach` produces the expected chunks in order.
- Add a test verifying cancellation mid-stream: cancel after the first chunk, verify `OperationCanceledException` is thrown.
- Document the `FakeChatClient` API in `docs/reference/testing-fakes.md`.

**Owner:** `zvec-code-reviewer-expert` + `zvec-rag-pipeline-expert`. **Effort:** 4 hours.

---

### Finding 9.3 — `ZVec.Rag.Testing` namespace packaging ambiguity

**Evidence:**
- Master Spec Story 2.4.2: "Package `ZVec.Rag.Testing` namespace."
- README package table: lists 5 packages (`ZVec.Extensions.VectorData`, `ZVec.Rag`, `ZVec.Rag.Template`, `ZVec.Rag.LLamaSharp`, `ZVec.Rag.ONNX`). `ZVec.Rag.Testing` is not listed.
- No doc specifies: is `ZVec.Rag.Testing` a separate NuGet package, a namespace within `ZVec.Rag`, or a folder structure?

**Why it matters:** Three plausible interpretations:

1. **Separate NuGet package** (`ZVec.Rag.Testing`): Test projects reference it via `<PackageReference>`. Clean separation; testing helpers don't bloat the runtime package. But the README doesn't list it — users won't know it exists.

2. **Namespace within `ZVec.Rag`** (`ZVec.Rag.Testing.DeterministicEmbedder`): Test projects reference `ZVec.Rag` itself. But then `DeterministicEmbedder` is in the runtime package, polluting the API surface. Users may accidentally use it in production.

3. **Internal namespace with `InternalsVisibleTo`**: Only the `ZVec.Rag.Tests` project can access it. Users cannot use `DeterministicEmbedder` in their own tests — they must write their own fakes. Defeats the "batteries-included" promise for testing.

The plan says "Package `ZVec.Rag.Testing` namespace" — which suggests interpretation 1 or 2. But without the package in the README table, interpretation 1 is unclear.

**Concrete fix:**
- Make `ZVec.Rag.Testing` a separate NuGet package (`ZVec.Rag.Testing`), listed in the README package table as "Testing helpers (`DeterministicEmbedder`, `FakeChatClient`) for unit-testing RAG pipelines without real LLM/embedder backends."
- The package depends on `ZVec.Rag` (for the interfaces) but not on any LLM/embedder backend.
- Document in `docs/reference/testing-strategy.md`: "Add `ZVec.Rag.Testing` to your test project. Use `DeterministicEmbedder` and `FakeChatClient` for fast, deterministic unit tests."
- Add a `ZVec.Rag.Testing` quickstart in the README showing a 5-line unit test.

**Owner:** `zvec-architect-strategy-expert` + `zvec-rag-pipeline-expert`. **Effort:** 4 hours.

---

### Finding 9.4 — Snapshot testing and embedder model versioning

**Evidence:**
- Master Spec Story 2.4.3: "Add snapshot test suite using `Verify.Xunit` for prompt formatting and citation outputs."
- `Verify.Xunit` stores snapshot files (`.received.txt` vs `.verified.txt`) in the repo.
- No doc specifies: how are snapshots versioned when the embedder model changes?

**Why it matters:** Citation outputs depend on retrieval, which depends on embeddings. If the test uses `DeterministicEmbedder` (Finding 9.1), embeddings are stable across runs — snapshots are stable. But if a test uses a real embedder (integration test), embeddings depend on the embedder model version:
- `nomic-embed-text` v1 → v2: embeddings change; snapshots break.
- `text-embedding-3-small` → `text-embedding-3-large`: dimensionality changes; snapshots break.

For unit tests with `DeterministicEmbedder`, snapshots are stable. For integration tests with real embedders, snapshots must be versioned per embedder model — or the tests become brittle.

The plan doesn't distinguish. A maintainer who updates the embedder model in CI will see all snapshots fail — and either: (a) blindly accept all new snapshots (defeating the purpose), or (b) spend hours debugging whether the change is expected.

**Concrete fix:**
- For unit tests with `DeterministicEmbedder`: snapshots are stable; no versioning needed.
- For integration tests with real embedders: use `Verify.Xunit`'s "named snapshot" feature — `Verify(snapshotName: $"nomic-embed-text-v1")`. When the embedder changes, create a new named snapshot and document the migration.
- Add a CI check: if an integration test snapshot changes, fail the build and require manual review (don't auto-accept).
- Document the snapshot versioning policy in `docs/reference/testing-snapshots.md`.

**Owner:** `zvec-code-reviewer-expert`. **Effort:** 4 hours.

---

## 10. Final Verdict

### 10.1 Summary of NEW findings by severity

| Severity | Count | Findings |
|---|---|---|
| **Critical (blocks Phase 2 implementation)** | 6 | 1.1 (ISP), 1.4 (dedup), 2.1 (source generator vs reflection), 2.4 (dimensionality), 5.1 (EnsureSchema DDL), 6.4 (file locking) |
| **High (blocks v1.0 claim accuracy / production readiness)** | 11 | 1.2, 1.3, 1.5, 1.6, 2.2, 2.3, 2.5, 3.1, 3.2, 4.1, 4.2, 4.3, 5.4, 6.1, 6.5, 7.1, 8.1, 8.2 |
| **Medium (should fix before v1.0)** | 10 | 1.6, 2.5, 3.3, 3.4, 3.5, 4.4, 5.2, 5.3, 5.5, 6.2, 6.3, 7.2, 7.3, 8.3, 9.1, 9.2, 9.3, 9.4 |
| **Low (process / hygiene)** | 0 | (covered in prior review) |

### 10.2 What the updated docs get right

- **Strategic narrative is sharper.** The "no cloud, no Python, no kidding" pitch is reinforced with cross-navigation between packages.
- **Tokenizer strategy is more honest.** The README now explicitly names `Microsoft.ML.Tokenizers` and the pluggable `tryAGI/Tiktoken` adapter, rather than glossing over tokenization.
- **Cross-navigation in README package table** helps users understand which package to install for which need.
- **`IngestTextAsync` + `AskAsync` quickstart** is more complete than the previous single-endpoint demo — it shows the full RAG lifecycle.

### 10.3 New blocking conditions before Phase 2 implementation

**Must resolve before writing Phase 2 code:**

1. **Split `IRagPipeline` into `IRagIngestor` + `IRagRetriever` + `IRagGenerator`** (Finding 1.1). Writing a monolithic `RagPipeline` first and refactoring later will cascade through all tests and samples.

2. **Define `IngestTextAsync` deduplication semantics** (Finding 1.4). The default behavior (`Replace` vs `Append` vs `Skip`) affects the schema (needs `DeleteByDocId`), the API surface, and the README quickstart.

3. **Audit the source generator's relationship to `ZVecCollectionSchemaBuilder.From<T>()`** (Finding 2.1). If the generator can't bypass reflection, the "zero-reflection" claim must be revised before it's baked into the Verification Matrix.

4. **Design the v1.0 citation schema with all plausible string fields at create-time** (Finding 5.1). Adding string fields later requires collection recreation — forward-compatible design is mandatory.

5. **Document `StoragePath` multi-process file locking semantics** (Finding 6.4). Multi-instance ASP.NET Core deployments will corrupt the ZVec file if locking is exclusive and undocumented.

6. **Define the `AddZVecRag` → `AddZVecVectorStore` → `AddZVec` composition contract** (Finding 1.2). Without this, users will call them in wrong order or with inconsistent options, producing silent misconfigurations.

### 10.4 Verdict: **CONDITIONAL GO — Re-scope Phase 2 Opening**

The strategic thesis remains sound. The architectural foundation (ZVec.NET + M.E.VectorData + M.E.AI) is correct. The new findings are **architecture-level decisions**, not documentation polish — they must be resolved before Phase 2 implementation begins, not during.

**Phase 2 opening sprint (1 week, before any RAG pipeline code):**
- Day 1–2: Interface segregation design (Finding 1.1) + composition contract (Finding 1.2, 1.3).
- Day 3: Citation schema design (Finding 5.1, 5.2, 5.3, 5.4, 5.5) + embedder-dimensionality coupling (Finding 2.4).
- Day 4: Source generator audit (Finding 2.1) + filter translator closure-capture design.
- Day 5: File locking documentation (Finding 6.4) + cancellation contract (Finding 6.2) + streaming contract (Finding 6.3).

After this sprint, Phase 2 implementation can proceed with clear contracts. Without it, Phase 2 will produce code that needs rewriting in Phase 3 when the gaps surface as integration bugs.

**Re-baseline timeline impact:** Add 1 week to Phase 2 (5 weeks instead of 4). Total v1.0 timeline: 13–17 weeks (from strategic plan's 12–16). The blocker clearance from the prior review (2–3 weeks) stacks on top: 15–20 weeks total.

**Final note:** The new findings are deeper than the prior review's because they require reading the ZVec.NET source to verify. The plan's claims about AOT, schema, and source generation are not just undocumented — they are sometimes **technically impossible given ZVec.NET's current API surface** (e.g., `EnsureSchema` only adding nullable numeric columns blocks string field migration; `ZVecCollectionSchemaBuilder.From<T>()` being reflection-based blocks the "zero-reflection" claim). These are not fixable by better documentation — they require either ZVec.NET upstream changes or honest re-scoping of the RAG plan's claims. Address them before Phase 2, or accept that v1.0 will ship with claims that don't survive technical scrutiny.
