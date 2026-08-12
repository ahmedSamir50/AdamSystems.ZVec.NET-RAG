# ZVec.NET-RAG — Gap Registry

> Auto-maintained by `zvec-gap-detection-expert`. Do not edit manually unless overriding status.

## Active Gaps

| ID | Severity | Category | File | Status | Since | Last Checked |
|----|----------|----------|------|--------|-------|-------------|
| P2-E | P2 | dynamic_dictionary_collections_stubbed | src/ZVec.Extensions.VectorData/ZVecVectorizableRecordCollection.cs | OPEN | 2026-08-13 | 2026-08-13 |
| P2-F | P2 | vectordata_collection_definition_ignored | src/ZVec.Extensions.VectorData/ZVecVectorizableRecordCollection.cs | OPEN | 2026-08-13 | 2026-08-13 |
| P2-H | P2 | class_line_count_exceeds_500 | src/ZVec.Extensions.VectorData/ZVecFilterExpressionVisitor.cs | OPEN | 2026-08-13 | 2026-08-13 |
| P3-C | P3 | sample_app_not_smoke_tested | samples/ZVec.Rag.Console/ | OPEN | 2026-08-13 | 2026-08-13 |

## Fixed Gaps (Historical)

| ID | Severity | Original Date | Fixed Date | Fixed In Commit | How |
|----|----------|--------------|------------|-----------------|-----|
| P2-A | P2 | 2026-08-13 | 2026-08-13 | working-tree | ZVEC001 analyzer now matches `{ClassName}ZVecMetadataMapper` (the name the SG emits); false positives eliminated |
| P2-B | P2 | 2026-08-13 | 2026-08-13 | working-tree | Filter-only GetAsync reads vector dimension from `_typeModel.Vectors.FirstOrDefault().Dimension`; falls back to `ZVecConstants.DefaultVectorDimension` (768) only when type model is unavailable |
| P2-C | P2 | 2026-08-13 | 2026-08-13 | working-tree | HybridSearchAsync now resolves FTS field via `ResolveFullTextField()` which prefers `[ZVecFullTextSearch]` / `IsFullTextIndexed` properties; honors `HybridSearchOptions.AdditionalProperty` when supplied |
| P2-D | P2 | 2026-08-13 | 2026-08-13 | working-tree | Added `ZVecHybridSearchOptions<TRecord>` deriving from `HybridSearchOptions<TRecord>` with `RrfK` knob; HybridSearchAsync passes it to `ZVecRrfReranker.RankConstant` |
| P2-G | P2 | 2026-08-13 | 2026-08-13 | working-tree | docs/architecture/native-aot-memory.md updated to reflect actual 3-RID CI coverage (linux-x64, win-x64, osx-x64); mobile RIDs attributed to upstream ZVec.NET package CI |
| P3-A | P3 | 2026-08-13 | 2026-08-13 | working-tree | Created docs/guides/migration-from-inmemory.md (Epic 1.11); added to mkdocs.yml nav |
| P3-B | P3 | 2026-08-13 | 2026-08-13 | working-tree | ZVec.Extensions.VectorData.SourceGenerator.Tests added to ZVec.NET-RAG.slnx and to quality-gate.yml test run; CS8892 suppressed to match other test projects |
| P3-D | P3 | 2026-08-13 | 2026-08-13 | working-tree | ZVec.NET-RAG-project-plan.md Epic 0.2 + Epic 1.1/1.2/1.3/1.6/1.8/1.10/1.11 checkboxes updated to reflect actual implementation; partial items (1.4, 1.5, 1.7, 1.9) annotated with status notes |
| P2-I | P2 | 2026-08-13 | 2026-08-13 | working-tree | ZVecVectorizableRecordCollection.cs refactored into partial classes (Schema.cs + Mapping.cs); main file reduced from 607 to 446 lines, under the 500-line CI cap |
| NC-2 | P2 | 2026-08-13 | 2026-08-13 | working-tree | DateTime + DateTimeOffset ContainAny dispatch verified by tests; object fallback proven for all BCL types |
| NC-3 | P2 | 2026-08-13 | 2026-08-13 | working-tree | TryGetRecordCollectionProperty rejects nested member access rooted at record parameter with ZVecFilterTranslationException; closure-variable access preserved |
| NC-4 | P3 | 2026-08-13 | 2026-08-13 | working-tree | RejectUserDefinedConversionExpression guard verified on all paths; BCL conversion whitelist test added proving no over-rejection |
| N-1/A-1 | P2 | 2026-08-12 | 2026-08-13 | working-tree | OptimizeAndReopenAsync minimize lock window; optimize runs outside lock, dispose-then-reopen inside lock (ZVec single-handle constraint), lazy reopen recovery; documented + lazy-recover test added |
| G-5 | P1 | 2026-08-12 | 2026-08-13 | working-tree | samples/ZVec.Rag.Console project: ingest + vectorized search + filtered hybrid search; runs end-to-end; added to .slnx |
| NC-1 | P1 | 2026-08-13 | 2026-08-13 | working-tree | OptimizeAndReopenAsync assigns new handle before nulling; lazy reopen via GetOrOpenNativeCollection() recovers on failure |
| NC-5 | P2 | 2026-08-13 | 2026-08-13 | working-tree | ContainAny non-string tests added: Int, Guid, Long collection contains |
| NC-6 | P3 | 2026-08-13 | 2026-08-13 | working-tree | ZVecFilterErrorCode enum + wired into ZVecFilterTranslationException.ErrorCode |
| N-3 | P2 | 2026-08-12 | 2026-08-13 | working-tree | ZVec.Extensions.VectorData.Analyzers project: ZVEC001 + ZVEC002 Roslyn diagnostics |
| G-3 | P2 | 2026-08-12 | 2026-08-13 | working-tree | ZVec.AotTestApp + quality-gate.yml aot-smoke/trim-warning-smoke jobs |
| G-1 | P2 | 2026-08-12 | 2026-08-13 | working-tree | Conformance tests added: SearchAsync_ZeroVector, SearchAsync_MaxDimensionVector, ConcurrentReadWriteStress_NoDataCorruption |
| N-2 | P2 | 2026-08-12 | 2026-08-13 | 6a53f7d | IsFullTextIndexedProperty() implements precedence: [ZVecFullTextSearch] > [VectorStoreData(IsFullTextIndexed)] |
| N-4 | P3 | 2026-08-12 | 2026-08-13 | 6a53f7d | Deleted VectorRecordAttributeReflectionTests.cs (test overlap eliminated) |
| G-2 | P1 | 2026-08-12 | 2026-08-12 | 1e282dd | ZVecFullTextSearchAttribute + hybrid search rewrite |
| G-4 | P1 | 2026-08-12 | 2026-08-12 | 1e282dd | OptimizeAndReopenAsync() method added |
| G-3-partial | P2 | 2026-08-12 | 2026-08-12 | 1e282dd | [RequiresUnreferencedCode] annotations added to reflection paths |

## Phase 2 Design Gaps (Tracked — Do Not Block)

| ID | Severity | Category | Doc Status |
|----|----------|----------|------------|
| D-1 | P2 | no_rag_evaluation_framework | IRagEvaluator specified in rag-pipeline.md |
| D-2 | P2 | cross_encoder_reranking_deferred | ICrossEncoderReranker + LlmReranker specified |
| D-3 | P2 | no_embedding_migration_strategy | IRagMigrationManager specified |
| D-4 | P2 | citation_chunk_id_undefined | Specified: SHA256(doc_uri \| strategy_id \| chunk_index) |
| D-5 | P2 | security_sanitizer_interface_only | Still interface-only |
| D-6 | P2 | batch_ingestion_topology_undefined | Bounded-channel dataflow graph specified |

## Status Legend

- **OPEN** — Gap exists, not yet addressed.
- **PartiallyFixed** — Some progress made, but gap not fully closed.
- **Fixed** — Gap resolved; moved to historical table.
- **Reopened** — Previously fixed gap reintroduced in a later change.
