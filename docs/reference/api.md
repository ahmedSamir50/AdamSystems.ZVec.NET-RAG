# API Reference

Complete API reference surface for `ZVec.Extensions.VectorData` and `ZVec.Rag`.

## `ZVec.Extensions.VectorData` (namespaces)

| Namespace | Key types |
|-----------|-----------|
| `ZVec.Extensions.VectorData.Store` | `ZVecVectorStore`, `ZVecVectorStoreOptions` |
| `ZVec.Extensions.VectorData.Collection` | `ZVecVectorizableRecordCollection<TRecord, TKey>` |
| `ZVec.Extensions.VectorData.Filter` | `ZVecFilterExpressionVisitor`, `ZVecFilterRecordModel` |
| `ZVec.Extensions.VectorData.Mapping` | `IZVecRecordMapper<T>`, `ZVecRecordMapperRegistry`, `ZVecCollectionSchemaRegistry`, `ZVecVectorDataSchemaBuilder` |
| `ZVec.Extensions.VectorData.Hybrid` | `ZVecHybridSearchOptions<TRecord>` |
| `Microsoft.Extensions.DependencyInjection` | `AddZVecVectorStore` extension |
| `ZVec.Extensions.VectorData.Constants` | `ZVecConstants`, `ZVecWellKnownMemberNames`, `ZVecDirectoryNames`, `ZVecErrorMessages` |

### Store (`ZVec.Extensions.VectorData.Store`)

- **`ZVecVectorStore`**: `IVectorStore` implementation backed by `IZvecFactory`.
- **`ZVecVectorStoreOptions`**: Configuration options (`StoragePath`, `MaxConcurrentNativeCalls`, optional custom `IZvecFactory`).

### Collection (`ZVec.Extensions.VectorData.Collection`)

- **`ZVecVectorizableRecordCollection<TRecord, TKey>`**: `IVectorizableRecordCollection` + `IKeywordHybridSearchable<TRecord>`.

### Filter (`ZVec.Extensions.VectorData.Filter`)

- **`ZVecFilterExpressionVisitor`**: AST visitor translating `Expression<Func<TRecord, bool>>` predicates to `ZVecFilterBuilder` (partials under `Filter/`). Supports 12 operators including relational, logical, `ContainAny`, `In`, direct boolean members, and null checks.
- **`ZVecFilterTranslationException`**: Translation failure with structured **`ZVecFilterErrorCode`**.

### Constants & analyzers

- **`ZVecErrorMessages`**, **`ZVecWellKnownMemberNames`**, **`ZVecDirectoryNames`**: Zero-magic-string helpers under `Constants/`.
- **`ZVec.Extensions.VectorData.Analyzers`**: Roslyn analyzers **`ZVEC001`** / **`ZVEC002`**.

## `ZVec.Rag`

- **`IRagPipeline`**: Primary RAG orchestrator.
- **`RagChunk`**: Streamed response chunk containing text and citations.
- **`Citation`**: Source document attribution (`SourceDoc`, `Page`, `Offset`, `Score`).
