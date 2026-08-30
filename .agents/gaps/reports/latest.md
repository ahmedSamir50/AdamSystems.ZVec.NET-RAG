# Spec Lock Report — 2026-08-30 (post-WRITE Stories 2.7 / 2.8)

```yaml
date: 2026-08-30
gates:
  write_allowed: true
  blocking_gaps: []
  warning_gaps:
    - D-7
    - D-8
    - D-9
    - NC-DENSE-FTS-ZERO
    - P2-E
    - P3-C
```

## Summary

Stories **2.7** (pipeline AOT via `ZVec.Rag.AotTestApp` + `rag-aot-smoke`) and **2.8** (`IRagEvaluator` / `DeterministicEvaluator` / `SemanticTestEmbedder`) are shipped. **D-1** closed. **D-3** remains deferred post-2.8 / pre-v1.1 (owner: `zvec-rag-pipeline-expert`). **NC-DENSE-FTS-ZERO** is PartiallyFixed: `DenseScore` from stored-vector cosine; `FtsScore` still `0`.

Public docs (`README.md`, `docs/**`) must not use the internal word `harness`. Implementation loop step 0 is **PLAN** (Allowed/Forbidden) before WRITE.

## Checklist

| Section | Result |
|---------|--------|
| 1. Three-file alignment | Pass |
| 2. Wiki vs live API | Pass |
| 3. Engine plumbing | Pass |
| 4. AOT claim vs `*AotTestApp` | Pass — pipeline AOT claimed only for text ingest + retrieve + Ask; no PDF/SSE/LLamaSharp |
| 5. RAG intra-spec | Pass |
| 6. Open D-* | D-1 closed. D-3 deferred with owner. D-7/D-8 post-v1. D-9 = Story 2.9. |
| 7. G1–G5 | Pass |
| 8–9. Story walk / S-* | Pass |

## write_allowed rationale

No P1 spec contradictions. Remaining `D-*` block only their own epics.
