# API Reference

Complete API reference surface for `ZVec.Extensions.VectorData` and `ZVec.Rag`.

## `ZVec.Extensions.VectorData` (namespaces)

| Namespace | Key types |
|-----------|-----------|
| `ZVec.Extensions.VectorData.Store` | `ZVecVectorStore`, `ZVecVectorStoreOptions` |
| `ZVec.Extensions.VectorData.Collection` | `ZVecVectorizableRecordCollection<TRecord, TKey>`, `ZVecScoreNormalizer` |
| `ZVec.Extensions.VectorData.Filter` | `ZVecFilterExpressionVisitor`, `ZVecFilterRecordModel` |
| `ZVec.Extensions.VectorData.Mapping` | `IZVecRecordMapper<T>`, `ZVecRecordMapperRegistry`, `ZVecCollectionSchemaRegistry`, `ZVecVectorDataSchemaBuilder`, `ZVecVectorIndexResolver` |
| `ZVec.Extensions.VectorData.Hybrid` | `ZVecHybridSearchOptions<TRecord>` |
| `Microsoft.Extensions.DependencyInjection` | `AddZVecVectorStore` extension |
| `ZVec.Extensions.VectorData.Constants` | `ZVecConstants`, `ZVecWellKnownMemberNames`, `ZVecDirectoryNames`, `ZVecErrorMessages`, `ZVecManifestFileNames` |
| `ZVec.Extensions.VectorData.Manifest` | `ZVecIndexManifest`, `ZVecIndexManifestManager`, `ZVecEmbedderMismatchException`, `ZVecManifestException` |

### Store (`ZVec.Extensions.VectorData.Store`)

- **`ZVecVectorStore`**: `IVectorStore` implementation backed by `IZvecFactory`.
- **`ZVecVectorStoreOptions`**: Configuration options (`StoragePath`, `ModelId`, `MaxConcurrentNativeCalls`, `EnableMmap`, `ReadOnly`, `MemoryLimitMb`, `DefaultQuantizeType`, optional custom `IZvecFactory`).

### Collection (`ZVec.Extensions.VectorData.Collection`)

- **`ZVecVectorizableRecordCollection<TRecord, TKey>`**: `IVectorizableRecordCollection` + `IKeywordHybridSearchable<TRecord>`.
- **`ZVecScoreNormalizer`**: Converts native dense-query distances to VectorData similarity scores (hybrid RRF scores are not re-normalized).

### Manifest (`ZVec.Extensions.VectorData.Manifest`)

- **`ZVecIndexManifestManager`**: Writes and validates `zvec_index_manifest.json` on collection open (atomic `*.tmp` + `File.Replace`).
- **`ZVecEmbedderMismatchException`**: Model/dimension/quantize/storage dtype mismatch with remediation guidance.
- **`ZVecManifestException`**: Missing or corrupt manifest (`ZVecManifestFailureReason`).

### Filter (`ZVec.Extensions.VectorData.Filter`)

- **`ZVecFilterExpressionVisitor`**: AST visitor translating `Expression<Func<TRecord, bool>>` predicates to `ZVecFilterBuilder` (partials under `Filter/`). Supports 12 operators including relational, logical, `ContainAny`, `In`, direct boolean members, and null checks.
- **`ZVecFilterTranslationException`**: Translation failure with structured **`ZVecFilterErrorCode`**.

### Constants & analyzers

- **`ZVecErrorMessages`**, **`ZVecWellKnownMemberNames`**, **`ZVecDirectoryNames`**: Zero-magic-string helpers under `Constants/`.
- **`ZVec.Extensions.VectorData.Analyzers`**: Roslyn analyzers **`ZVEC001`** / **`ZVEC002`**.

## `ZVec.Rag`

| Namespace | Key types |
|-----------|-----------|
| `ZVec.Rag.Abstractions` | `IRagIngestor`, `IRagRetriever`, `IRagGenerator`, `IRagPipeline`, `IRagDocumentReader`, `IZVecTextChunker` |
| `ZVec.Rag` | `RagPipeline` |
| `ZVec.Rag.Generation` | `ContextPacker`, `RagGenerator` |
| `ZVec.Rag.Ingestion` | `RagIngestor`, `PlainTextDocumentReader`, `TokenTextChunker`, `MarkdownHeadingChunker`, `SentenceTextChunker`, `ZVecChunkIdGenerator`, `ZVecTokenizerResolver`, `ZVecTextChunkerRegistry` |
| `ZVec.Rag.Pdf` | `PdfDocumentReader`, `CompositeRagDocumentReader`, `AddZVecRagPdf` |
| `ZVec.Rag.Retrieval` | `RagRetriever` |
| `ZVec.Rag.Streaming` | `RagSseEndpointExtensions` (`MapRagSseEndpoint`) |
| `ZVec.Rag.Models` | `Citation`, `RagChunk`, `IngestionResult`, `IngestOptions`, `IngestTextRequest`, `TextChunk`, `DuplicateMode`, `CitationOrder`, `ContextPackingStrategy` |
| `ZVec.Rag.Options` | `ZVecRagOptions` |
| `ZVec.Rag.Schema` | `ZVecRagRecordV1`, `ZVecRagSectionSummaryV1` |
| `ZVec.Rag.Exceptions` | `ZVecRagInitializationException` |
| `ZVec.Rag.Internal` | `RagCollectionProvider` (scoped native handle; releases on scope dispose) |
| `Microsoft.Extensions.DependencyInjection` | `AddZVecRag`, `AddTokenChunker`, `AddMarkdownChunker`, `AddSentenceChunker`, `AddZVecRagPdf` |

### Document readers

- **`IRagDocumentReader.ReadAsync(stream, contentType, cancellationToken)`**: Format ACL entry point. Core ships `PlainTextDocumentReader` (text/markdown). PDF requires `ZVec.Rag.Pdf` and `AddZVecRagPdf()`.
- **`ZVecRagConstants.PdfContentType`**: `application/pdf`.
- **`PdfDocumentReader`**: PdfPig text extract only (no table parsing). Trim-annotated; not in Native AOT smoke.
- **`AddZVecRagPdf()`**: Replaces `IRagDocumentReader` with `CompositeRagDocumentReader` routing PDF vs text.

### Ingestion (`IRagIngestor`)

- **`IngestTextAsync`**: Chunk, embed, and upsert plain text or markdown.
- **`IngestDocumentAsync`**: Stream ingest with `contentType` (`text/plain`, `text/markdown`, or `application/pdf` when `ZVec.Rag.Pdf` is installed).
- **`IngestBatchAsync`**: Sequential multi-document ingest; auto-runs **`OptimizeAsync`** after the batch.
- **`OptimizeAsync`**: Delegates to `ZVecVectorizableRecordCollection.OptimizeAndReopenAsync` (native optimize outside lock; dispose-reopen inside `lock (_initLock)`).

### Chunking ACL

- **`IZVecTextChunker`**: Sync `IEnumerable<TextChunk>` (text + char offset). Output is pushed into bounded channels — never `Task.Run`.
- **`TokenTextChunker`**: Default strategy `token-v1` (512 max tokens, 64 overlap via `AddTokenChunker`).
- **`MarkdownHeadingChunker`**: Strategy `markdown-heading-v1`; line `#` heading boundaries + token cap.
- **`SentenceTextChunker`**: Strategy `sentence-v1`; no mid-sentence splits.
- **`DuplicateMode`**: `Replace` (delete all `SourceDoc` chunks, paged `GetAsync`), `Append` (`max(ChunkIndex)+1`), `Skip` (no-op if any chunk exists).

### Retrieval & citations

- **`IRagPipeline`**: Composite facade (`IRagIngestor` + `IRagRetriever` + `IRagGenerator`).
- **`RagChunk`**: Streamed response chunk (`Text`, `Citations`, `IsFinal`, `Usage`).
- **`Citation`**: `SourceDoc`, `SourceUri`, `SourceHash`, `Page`, `Offset`, `ChunkIndex`, `ChunkId`, `RankScore`, `DenseScore`, `FtsScore`, optional `SectionSummaryId` / `SectionSummary` (packing only; `Text` stays child chunk text). Hybrid search sets `RankScore` from fused RRF. `DenseScore` is cosine similarity (clamped to `[0, 1]`) between the query embedding and the **stored** chunk vector when hybrid search returns it (`IncludeVectors = true`). Empty stored vector → `DenseScore = 0` (retrieve does not re-embed chunk text). `FtsScore` remains `0` until connector exposes per-leg FTS scores.
- **`CitationOrder`**: `ScoreDescending` (default), `ChunkOrderAscending`, `SourceDocThenChunkOrder`, `PageAscending`, `None`. UI list order; independent of `ContextPacker` prompt order.

### SSE

- **`MapRagSseEndpoint(pattern)`**: Maps GET endpoint; reads `question` query string; writes `text/event-stream`; `FlushAsync` after each chunk; links `HttpContext.RequestAborted` into `AskAsync`. JSON payload uses **camelCase** (`text`, `isFinal`, `citations`, nested citation fields). Requires `FrameworkReference Microsoft.AspNetCore.App` on `ZVec.Rag`; trim-annotated in `Streaming/`.

### Options

- **`ZVecRagOptions`**: `StoragePath`, `Embedder`, `Chat`, `RrfK`, `MaxContextTokens`, `GenerationReserveTokens`, `GenerateSummaries` (retrieve/pack; default false), `CollectionName` (default rag_chunks), `SummaryCollectionName` (null = resolve), `TokenizerEncoding`, `TokenizerModelPath`, nested `ZVecVectorStoreOptions`.
- **Summary collection resolve:** if `SummaryCollectionName` is set (non-whitespace), use it; else if `CollectionName` is `rag_chunks`, use `rag_section_summaries`; else use `CollectionName` + `_summaries`.
- **`IngestOptions`**: `GenerateSummaries` (ingest; default `false`), `MaxSummaryTokens` (default `128`), `SummarySectionMaxTokens` (default `2048`), `OnDuplicate`, `SourceUri`, `Page`, `Chunker`.
- **`ZVecRagConstants.SectionSummaryCollectionName`**: `rag_section_summaries`.
- **`ZVecRagSectionSummaryV1`**: `SectionSummaryId`, `SourceDoc`, `SourceUri`, `SectionIndex`, `Summary`, `DenseVector`.
- **`ZVecRagRecordV1.SectionSummaryId`**: indexed FK to parent section summary when ingest summaries are enabled.
- **`ZVecRagInitializationException`**: Wraps embedder stamp mismatch with delete-path / `IRagMigrationManager` remediation.

### Connector (`ZVecVectorizableRecordCollection`)

- **`ReleaseNativeHandle()`**: Disposes native read-write handle without deleting on-disk data. Called by scoped `RagCollectionProvider` on scope dispose so subsequent scopes can reopen the same collection path.

## `ZVec.Rag.Testing`

| Type | Purpose |
|------|---------|
| `DeterministicEmbedder` | Hash-based `IEmbeddingGenerator` for fast CI tests |
| `FakeChatClient` | Dual streaming/non-streaming `IChatClient` fake (`LastStreamingCallWasCanceled`, `TokensYielded`, optional `UsageDetails` on final streaming update) |

## `ZVec.Rag.LLamaSharp`

| Namespace | Key types |
|-----------|-----------|
| `ZVec.Rag.LLamaSharp` | `LLamaSharpChatClient`, `LLamaSharpEmbedder`, `LLamaSharpOptions`, `LLamaSharpConstants` |
| `Microsoft.Extensions.DependencyInjection` | `AddZVecRagLLamaSharp` |

- **`LLamaSharpChatClient`**: `IChatClient` over local GGUF weights (`GetResponseAsync`, `GetStreamingResponseAsync`). Honors `CancellationToken`. `[RequiresUnreferencedCode]` — not in `ZVec.Rag.AotTestApp`.
- **`LLamaSharpEmbedder`**: `IEmbeddingGenerator<string, Embedding<float>>` when the GGUF exposes embeddings.
- **`AddZVecRagLLamaSharp`**: Registers singleton chat + embed adapters; sets `ZVecRagOptions.Chat` / `Embedder` when null.

## `ZVec.Rag.ONNX`

| Namespace | Key types |
|-----------|-----------|
| `ZVec.Rag.ONNX` | `OnnxEmbedder`, `OnnxEmbedderOptions`, `OnnxEmbeddingModelKind`, `ClipImagePreprocessor`, `OnnxConstants` |
| `ZVec.Rag.ONNX.Schema` | `ZVecRagMultimodalRecordV1` (512-d CLIP; indexed `SourceKind` = `text` \| `image`) |
| `Microsoft.Extensions.DependencyInjection` | `AddZVecRagOnnxEmbedder` |

- **`OnnxEmbedder`**: Text embeddings for `MiniLm`, `EmbeddingGemma`, `ClipText`. `EmbedImageAsync` when `ModelKind == ClipText` and `VisionModelPath` is set.
- **`ClipImagePreprocessor`**: ImageSharp NCHW tensor with documented CLIP mean/std in `OnnxConstants`.
- **`AddZVecRagOnnxEmbedder`**: Registers singleton `OnnxEmbedder`; assigns `ZVecRagOptions.Embedder` only when dimensions match `ZVecRagRecordV1.DefaultDimensions` (768).

## `ZVec.Rag.Telemetry`

| Type | Purpose |
|------|---------|
| `ZVecRagTelemetry.ActivitySource` | OpenTelemetry activities: `ingest`, `retrieve`, `generate` |
| `ZVecRagTelemetry.Meter` | `zvec.rag.tokens` counter; `zvec.rag.stage.duration` histogram (ms) |

Tag keys: `stage` (`ingest` \| `retrieve` \| `generate` \| `embed` \| `chat`), `direction` (`input` \| `output`) for token counter. Host apps subscribe via `AddOpenTelemetry().WithTracing(t => t.AddSource("ZVec.Rag")).WithMetrics(m => m.AddMeter("ZVec.Rag"))` — not shipped inside `ZVec.Rag`.
