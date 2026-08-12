# API Reference

Complete API reference surface for `ZVec.Extensions.VectorData` and `ZVec.Rag`.

## `ZVec.Extensions.VectorData`

- **`ZVecVectorStore`**: `IVectorStore` implementation backed by `IZvecFactory`.
- **`ZVecVectorStoreOptions`**: Configuration options for vector store storage path and factory registration.
- **`ZVecVectorizableRecordCollection<TRecord, TKey>`**: `IVectorizableRecordCollection` implementation.
- **`IZVecRecordMapper<TRecord>`**: Interface for zero-reflection POCO record mapping (`ToDoc`, `FromDoc`).
- **`ZVecRecordMapperRegistry`**: Process-wide registry for SG-emitted mappers populated via `[ModuleInitializer]`.
- **`ZVecFilterExpressionVisitor`**: AST visitor translating `Expression<Func<TRecord, bool>>` predicates to `ZVecFilterBuilder`. Supports 12 operators including relational, logical, `ContainAny` (`x.Tags.Contains(value)`), `In` (`values.Contains(x.Field)`), and null checks. Throws `ZVecFilterTranslationException` with remediation for unsupported string methods.

## `ZVec.Rag`

- **`IRagPipeline`**: Primary RAG orchestrator.
- **`RagChunk`**: Streamed response chunk containing text and citations.
- **`Citation`**: Source document attribution (`SourceDoc`, `Page`, `Offset`, `Score`).
