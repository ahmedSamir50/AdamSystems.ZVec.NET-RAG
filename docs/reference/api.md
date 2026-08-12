# API Reference

Complete API reference surface for `ZVec.Extensions.VectorData` and `ZVec.Rag`.

## `ZVec.Extensions.VectorData`

- **`ZVecVectorStore`**: `IVectorStore` implementation backed by `IZvecFactory`.
- **`ZVecVectorStoreOptions`**: Configuration options for vector store storage path and factory registration.
- **`ZVecVectorizableRecordCollection<TRecord, TKey>`**: `IVectorizableRecordCollection` implementation.
- **`IZVecRecordMapper<TRecord>`**: Interface for zero-reflection POCO record mapping (`ToDoc`, `FromDoc`).
- **`ZVecRecordMapperRegistry`**: Process-wide registry for SG-emitted mappers populated via `[ModuleInitializer]`.
- **`ZVecFilterExpressionVisitor`**: AST visitor translating `Expression<Func<TRecord, bool>>` predicates to `ZVecFilterBuilder`. Supports 12 operators including relational, logical, `ContainAny` (`x.Tags.Contains(value)` with typed dispatch for `int`, `long`, `float`, `double`, `bool`, `string`, `Guid`, `DateTime`, and `DateTimeOffset`), `In` (`values.Contains(x.Field)`), and null checks. Rejects user-defined implicit/explicit conversion operators in filter expressions.
- **`ZVecFilterTranslationException`**: Translation failure exception exposing structured **`ZVecFilterErrorCode`** for programmatic handling (`UnsupportedExpression`, `UnsupportedStartsWith`, `UnsupportedEndsWith`, `UnsupportedRegex`, `UnsupportedStringContains`, `UnsupportedUserDefinedConversion`).
- **`ZVecErrorMessages`**: Strongly-typed error formatting helpers (including field-aware remediation messages such as `UnsupportedStartsWithMethod(fieldName)`).
- **`ZVec.Extensions.VectorData.Analyzers`**: Roslyn diagnostic analyzers emitting **`ZVEC001`** (missing source-generated mapper) and **`ZVEC002`** (reflection outside approved fallback paths). Severity is configurable via `.editorconfig`.

## `ZVec.Rag`

- **`IRagPipeline`**: Primary RAG orchestrator.
- **`RagChunk`**: Streamed response chunk containing text and citations.
- **`Citation`**: Source document attribution (`SourceDoc`, `Page`, `Offset`, `Score`).
