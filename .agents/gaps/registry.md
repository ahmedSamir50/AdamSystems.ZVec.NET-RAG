# ZVec.NET-RAG — Gap Registry

> Auto-maintained by `zvec-gap-detection-expert`. Do not edit manually unless overriding status.

## Active Gaps

| ID | Severity | Category | File | Status | Since | Last Checked |
|----|----------|----------|------|--------|-------|-------------|
| _None — all active gaps closed._ | | | | | | |

## Fixed Gaps (Historical)

| ID | Severity | Original Date | Fixed Date | Fixed In Commit | How |
|----|----------|--------------|------------|-----------------|-----|
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
