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
| `ZVec.Rag.Abstractions` | `IRagIngestor`, `IRagRetriever`, `IRagGenerator`, `IRagPipeline` |
| `ZVec.Rag` | `RagPipeline` |
| `ZVec.Rag.Generation` | `ContextPacker`, `RagGenerator` |
| `ZVec.Rag.Ingestion` | `RagIngestor`, `ZVecChunkIdGenerator` |
| `ZVec.Rag.Retrieval` | `RagRetriever` |
| `ZVec.Rag.Models` | `Citation`, `RagChunk`, `IngestionResult`, `IngestOptions`, `IngestTextRequest`, `CitationOrder`, `ContextPackingStrategy` |
| `ZVec.Rag.Options` | `ZVecRagOptions` |
| `ZVec.Rag.Schema` | `ZVecRagRecordV1` |
| `ZVec.Rag.Exceptions` | `ZVecRagInitializationException` |
| `Microsoft.Extensions.DependencyInjection` | `AddZVecRag` extension |

- **`IRagPipeline`**: Composite facade (`IRagIngestor` + `IRagRetriever` + `IRagGenerator`).
- **`RagChunk`**: Streamed response chunk (`Text`, `Citations`, `IsFinal`, `Usage`).
- **`Citation`**: Source attribution (`SourceDoc`, `SourceUri`, `SourceHash`, `Page`, `Offset`, `ChunkIndex`, `ChunkId`, `RankScore`, `DenseScore`, `FtsScore`).
- **`ZVecRagOptions`**: `StoragePath`, `Embedder`, `Chat`, `RrfK`, `MaxContextTokens`, `GenerationReserveTokens`, nested `ZVecVectorStoreOptions`.
- **`ZVecRagInitializationException`**: Wraps embedder stamp mismatch with delete-path / `IRagMigrationManager` remediation.

## `ZVec.Rag.Testing`

| Type | Purpose |
|------|---------|
| `DeterministicEmbedder` | Hash-based `IEmbeddingGenerator` for fast CI tests |
| `FakeChatClient` | Dual streaming/non-streaming `IChatClient` fake |
