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
| `ZVec.Rag.Retrieval` | `RagRetriever` |
| `ZVec.Rag.Streaming` | `RagSseEndpointExtensions` (`MapRagSseEndpoint`) |
| `ZVec.Rag.Models` | `Citation`, `RagChunk`, `IngestionResult`, `IngestOptions`, `IngestTextRequest`, `TextChunk`, `DuplicateMode`, `CitationOrder`, `ContextPackingStrategy` |
| `ZVec.Rag.Options` | `ZVecRagOptions` |
| `ZVec.Rag.Schema` | `ZVecRagRecordV1` |
| `ZVec.Rag.Exceptions` | `ZVecRagInitializationException` |
| `ZVec.Rag.Internal` | `RagCollectionProvider` (scoped native handle; releases on scope dispose) |
| `Microsoft.Extensions.DependencyInjection` | `AddZVecRag`, `AddTokenChunker`, `AddMarkdownChunker`, `AddSentenceChunker` |

### Ingestion (`IRagIngestor`)

- **`IngestTextAsync`**: Chunk, embed, and upsert plain text or markdown.
- **`IngestDocumentAsync`**: UTF-8 stream ingest with `contentType` (`text/plain`, `text/markdown`).
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
- **`Citation`**: `SourceDoc`, `SourceUri`, `SourceHash`, `Page`, `Offset`, `ChunkIndex`, `ChunkId`, `RankScore`, `DenseScore`, `FtsScore`. Hybrid search sets `RankScore` from fused RRF; `DenseScore`/`FtsScore` remain `0` until connector exposes per-leg scores.
- **`CitationOrder`**: `ScoreDescending` (default), `ChunkOrderAscending`, `SourceDocThenChunkOrder`, `PageAscending`, `None`. UI list order; independent of `ContextPacker` prompt order.

### SSE

- **`MapRagSseEndpoint(pattern)`**: Maps GET endpoint; reads `question` query string; writes `text/event-stream`; `FlushAsync` after each chunk; links `HttpContext.RequestAborted` into `AskAsync`. Requires `FrameworkReference Microsoft.AspNetCore.App` on `ZVec.Rag`; trim-annotated in `Streaming/`.

### Options

- **`ZVecRagOptions`**: `StoragePath`, `Embedder`, `Chat`, `RrfK`, `MaxContextTokens`, `GenerationReserveTokens`, `TokenizerEncoding`, `TokenizerModelPath`, nested `ZVecVectorStoreOptions`.
- **`ZVecRagInitializationException`**: Wraps embedder stamp mismatch with delete-path / `IRagMigrationManager` remediation.

### Connector (`ZVecVectorizableRecordCollection`)

- **`ReleaseNativeHandle()`**: Disposes native read-write handle without deleting on-disk data. Called by scoped `RagCollectionProvider` on scope dispose so subsequent scopes can reopen the same collection path.

## `ZVec.Rag.Testing`

| Type | Purpose |
|------|---------|
| `DeterministicEmbedder` | Hash-based `IEmbeddingGenerator` for fast CI tests |
| `FakeChatClient` | Dual streaming/non-streaming `IChatClient` fake (`LastStreamingCallWasCanceled`, `TokensYielded`) |
