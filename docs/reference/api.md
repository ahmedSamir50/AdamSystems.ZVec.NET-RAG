# API Reference

Complete API reference surface for `ZVec.Extensions.VectorData` and `ZVec.Rag`.

## `ZVec.Extensions.VectorData`

- **`ZVecVectorStore`**: `IVectorStore` implementation backed by `IZvecFactory`.
- **`ZVecVectorizableRecordCollection<TRecord, TKey>`**: `IVectorizableRecordCollection` implementation.
- **`ZVecFilterExpressionVisitor`**: AST visitor translating `VectorDataFilter` to `ZVecFilterBuilder`.

## `ZVec.Rag`

- **`IRagPipeline`**: Primary RAG orchestrator.
- **`RagChunk`**: Streamed response chunk containing text and citations.
- **`Citation`**: Source document attribution (`SourceDoc`, `Page`, `Offset`, `Score`).
