# ZVec.NET-RAG — Gap Registry

> Auto-maintained by `zvec-gap-detection-expert`. Do not edit manually unless overriding status.

## Active Gaps

| ID | Severity | Category | File | Status | Since | Last Checked |
|----|----------|----------|------|--------|-------|-------------|
| P2-E | P2 | dynamic_dictionary_collections_stubbed | src/ZVec.Extensions.VectorData/Collection/ZVecVectorizableRecordCollection.cs | OPEN | 2026-08-13 | 2026-08-30 |
| P3-C | P3 | sample_app_not_smoke_tested | samples/ZVec.Rag.Console/ | OPEN | 2026-08-13 | 2026-08-30 |
| NC-DENSE-FTS-ZERO | P2 | citation_dense_fts_scores_zeroed | src/ZVec.Rag/Retrieval/RagRetriever.cs | PartiallyFixed | 2026-08-30 | 2026-08-30 |

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
| P2-H | P2 | 2026-08-13 | 2026-08-26 | 9b54094 | ZVecFilterExpressionVisitor split into partial classes (Evaluation.cs + MethodCalls.cs); largest file 241 lines |
| P2-F | P2 | 2026-08-13 | 2026-08-26 | working-tree | BuildCollectionSchema precedence: SG registry → caller Definition (ZVecVectorDataSchemaBuilder) → reflection fallback; collection ctor defaults Definition from ZVecCollectionSchemaRegistry |
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
| DS-1 | P2 | 2026-08-26 | 2026-08-26 | working-tree | ZVecVectorStoreOptions: EnableMmap, ReadOnly, MemoryLimitMb, DefaultQuantizeType plumbed; ZVecVectorIndexResolver for FP16/INT8 |
| DS-2 | P2 | 2026-08-26 | 2026-08-26 | working-tree | mobile-memory-budget.md corrected: mmap+ReadOnly for shipped indexes; real ZVecQuantizeType API |
| DS-3 | P2 | 2026-08-26 | 2026-08-26 | working-tree | Second evaluation: LITM vs CitationOrder decoupled; Sample 03 Flat default; stamp QuantizeType; Tiktoken AOT gate; Channels not Task.Run |
| D-1 | P2 | 2026-08-26 | 2026-08-30 | working-tree | Story 2.8: `IRagEvaluator`, `DeterministicEvaluator`, `SemanticTestEmbedder`, in-repo seed fixtures |
| D-9 | P2 | 2026-08-26 | 2026-08-30 | working-tree | Story 2.9: optional section-summary helper (`GenerateSummaries` default OFF); `rag_section_summaries` collection; parallel union+boost retrieve; packer prepends summary outside `<retrieved_context>` |

## Phase 2 Design Gaps (Block WRITE of that epic)

These are **not** "do not block forever." They block **WRITE of the epic they belong to** until tasked in `project_tasks_implementation_plan.md` (or explicitly deferred with owner + story id). They do not block merge of unrelated connector work.

| ID | Severity | Category | Doc Status |
|----|----------|----------|------------|
| D-2 | P2 | cross_encoder_reranking_deferred | Explicitly deferred in Task 2.3.2 (post-v1.1); default `ZVecRrfReranker` |
| D-3 | P2 | no_embedding_migration_strategy | **Deferred post-Story 2.8 / pre-v1.1** (owner: `zvec-rag-pipeline-expert`); interface referenced in error strings/wiki only |
| D-4 | P2 | citation_chunk_id_undefined | Specified: SHA256(doc_uri \| strategy_id \| chunk_index) |
| D-5 | P2 | security_sanitizer_interface_only | Story 2.6 shipped: `IRagSecuritySanitizer` + `DefaultRagSecuritySanitizer` + prompt isolation |
| D-6 | P2 | batch_ingestion_topology_undefined | **Shipped topology:** bounded `System.Threading.Channels` via `IngestionChannelPump` (capacity 1024, wait-on-full); Task 2.2.3 forbids `Task.Run`. `IngestTextAsync` awaits same-call pipeline completion. **Not NATS:** broker/distributed ingest = post-v1 optional `IIngestBus`, not core v1. |
| D-7 | P2 | complex_doc_ingest | **Deferred post-v1** — Epic 8.7 (owner: `zvec-architect-strategy-expert`). Layout-aware readers (PDF tables, PPT slides, DOCX, Excel) emit parse tree **before** stamps. Additive schema: `HeadingPath` (indexed breadcrumb), `ParentChunkId` (indexed nullable heading/page/table node). `ContextPacker` may fetch parent by id later; **`ChunkId` formula unchanged**. Markdown `HeadingPath` = first reader of epic, not silent v1 schema change. Sample 02 PdfPig = text extract only. |
| D-8 | P2 | query_complexity | **Deferred post-v1** — Epic 8.8 (owner: `zvec-architect-strategy-expert`). Router, sub-questions, auto-retrieval filters. Blocks research-assistant epic until tasked. |

## Spec Gaps (S-*)

Locked findings from spec_lock / consultant restudies. Process gaps live here too.

| ID | Severity | Category | Status | Since | Notes |
|----|----------|----------|--------|-------|-------|
| S-LOOP | P1 | no_spec_lock_before_write | Fixed | 2026-08-26 | Gap detector was post-commit only. Closed: `spec_lock` trigger, `.agents/gaps/spec-lock.md`, `.cursor/rules/spec-lock.mdc` |
| S-LITM | P2 | litm_vs_citation_order | Fixed | 2026-08-26 | Tasks 2.1.3/2.3.2 + citation-schema.md (prompt order ≠ CitationOrder) |
| S-MOBILE-INT8 | P2 | sample03_int8_unmeasured | Fixed | 2026-08-26 | Task 3.2.3 Flat default; INT8 optional ≥0.95 Recall@K vs FP32 Flat |
| S-STAMP-DX | P2 | embedder_stamp_dx | Fixed | 2026-08-26 | Story 1.11 QuantizeType + Task 2.1.4 wrap; 1.11 numbering labeled |
| S-AOT-TOKEN | P2 | tokenizer_aot_path | Fixed | 2026-08-26 | 2.2.4 Tiktoken in-box; 2.7.1 must tokenize with Tiktoken |
| S-CHUNKER-SYNC | P2 | chunker_channels_not_taskrun | Fixed | 2026-08-26 | 2.2.1/2.2.3 Channels; no PDF in core tests |
| S-QUANT-REBUILD | P3 | quantize_requires_rebuild | Fixed | 2026-08-26 | Stamp QuantizeType; docs/guides/quantization.md |
| S-FACTORYOPTIONS | P1 | wiki_snippet_invented_member | Fixed | 2026-08-26 | migration-from-inmemory.md uses `MaxConcurrentNativeCalls` |
| S-111-WIKI-AOT | P2 | story_id_collision_aot_banner | Fixed | 2026-08-26 | native-aot-memory.md relabeled Connector AOT CI vs Story 1.11 stamp |
| S-D2-UNTASKED | P2 | cross_encoder_untasked | Fixed | 2026-08-26 | Task 2.3.2 explicitly defers D-2 ICrossEncoderReranker |
| S-RWLS-DRIFT | P2 | rwls_spec_vs_shipped_lock | Fixed | 2026-08-26 | Task 2.3.1 delegates to OptimizeAndReopenAsync; wiki aligned |
| S-SSE-CANCEL | P2 | sse_no_request_aborted | Fixed | 2026-08-26 | Task 2.3.3 + rag-pipeline.md + README |
| S-MANIFEST-ATOMIC | P2 | manifest_non_atomic | Fixed | 2026-08-26 | Task 1.11.2 tmp+replace; Missing/Corrupt vs mismatch |
| S-MAUI-UI-THREAD | P2 | maui_ui_thread_open | Fixed | 2026-08-26 | Task 3.2.3 + mobile-memory-budget.md background open |
| S-AOT-INGEST-ACL | P2 | aot_tokenizer_only | Fixed | 2026-08-26 | Task 2.2.3 DI factory; 2.7.1 full IngestTextAsync |
| S-SAMPLE03-EPIC5 | P2 | epic53_generic | Fixed | 2026-08-26 | project-plan Epic 5.3 detailed Sample 03 policy |
| S-CATEGORY-G | P1 | runtime_interop_harness_gap | Fixed | 2026-08-26 | spec-lock.md §7 + skill pushbacks G1–G5 |
| S-TEXTCHUNKER | P2 | itextchunker_stale_api | Fixed | 2026-08-27 | 2026-08-30: project-plan mermaid + package tree corrected; in-repo `IZVecTextChunker` only |
| S-RAG-STATUS-BANNER | P2 | rag_pipeline_status_stale | Fixed | 2026-08-30 | rag-pipeline.md banner: 2.7 + 2.8 shipped |
| S-SLIDING-CHUNKER | P3 | mermaid_lists_unshipped_chunker | Fixed | 2026-08-30 | README + rag-pipeline mermaid: Token / Markdown / Sentence only |
| S-NAIVE-RAG-HONESTY | P2 | readme_overclaims_advanced_rag | Fixed | 2026-08-30 | README "What this is / is not" + plan Epic 8.7/8.8; Liu-axis citations |
| S-README-OVERCLAIM | P1 | readme_nonexistent_packages | Fixed | 2026-08-27 | README lists only shipped packages; Template/LLamaSharp/ONNX marked Planned |
| S-EPIC34-MAP | P1 | epic_3_4_unlabeled | Fixed | 2026-08-27 | Story ID map labels added to both plan files |
| S-WIKI-HYBRID-API | P1 | wiki_invented_hybrid_api | Fixed | 2026-08-27 | score-semantics.md + hybrid-search-rrf.md aligned to `IKeywordHybridSearchable` / raw RRF |
| S-SG-METRIC-COSINE | P1 | sg_dense_metric_cosine_fallback | Fixed | 2026-08-27 | `ResolveDenseMetricType` reads SG schema; L2 integration test |
| S-SPEC-REPORT | P1 | no_spec_lock_artifact | Fixed | 2026-08-27 | `.agents/gaps/reports/2026-08-27-spec-lock.md` + `latest.md` |
| S-PLAN-ALIGN-CI | P2 | ci_no_plan_alignment | Fixed | 2026-08-27 | `plan_alignment_check.py` in gap-detection + quality-gate |
| S-RAG-AOT-HARNESS | P2 | pipeline_aot_harness_missing | Fixed | 2026-08-27 | `tests/ZVec.Rag.AotTestApp` + `rag-aot-smoke` CI job (Story 2.7.1) |
| S-SSE-FXREF | P2 | sse_framework_reference | Fixed | 2026-08-27 | MapRagSseEndpoint in ZVec.Rag/Streaming with FrameworkReference; trim annotated |
| S-INGEST-CALLER-ENUM | P2 | ingest_caller_continuation_docs | Fixed | 2026-08-27 | rag-pipeline.md async contract aligned with Channels + ForceYielding open; not Task.Run |
| NC-CA-CONNECTOR | P2 | connector_configure_await_hygiene | Fixed | 2026-08-27 | CAF on ZVecVectorizableRecordCollection CRUD/search awaits + ListCollectionNames Yield |
| S-EPIC34-MAP-DRIFT | P1 | epic34_map_notes_false | Fixed | 2026-08-27 | Implementation-plan map notes corrected; plan_alignment_check.py asserts inversion |
| S-RAG-SG-AOT | P1 | rag_schema_no_source_generator | Fixed | 2026-08-30 | Story 2.7.3: SG wired into `ZVec.Rag.csproj`; `ZVecRagRecordV1` AOT-clean schema via registry |

DS-1…DS-3 (mmap/quantize plumbing, mobile wiki, second-evaluation docs) remain in Fixed Gaps (Historical).

## Status Legend

- **OPEN** — Gap exists, not yet addressed.
- **PartiallyFixed** — Some progress made, but gap not fully closed.
- **Fixed** — Gap resolved; moved to historical table.
- **Reopened** — Previously fixed gap reintroduced in a later change.
