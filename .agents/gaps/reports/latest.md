# Spec Lock Report — 2026-08-27 (rebuilt verified audit remediation)

```yaml
date: 2026-08-27
gates:
  write_allowed: true
  blocking_gaps: []
  warning_gaps:
    - D-3
    - D-1
```

## Summary

Remediation closed P1 doc/plan contradictions **V1–V10**, P1 code bug **V20** (SG metric resolution), and P2 staleness **V11–V17**. Harness artifacts **V18** persisted. Registry **S-TEXTCHUNKER** re-opened until all three plan files agree (now aligned).

## P1 closures

| ID | Resolution |
|----|------------|
| V1 | Task 2.4.1 unchecked; SemanticTestEmbedder deferred to Story 2.8 |
| V2–V5 | README packages/quickstart/installation corrected to shipped APIs + Planned rows |
| V6–V7 | DataIngestion ACL wording removed; in-repo `IZVecTextChunker` documented |
| V8 | Epic 3/4 cross-reference labels added to both plan files |
| V9 | `score-semantics.md` hybrid snippet uses `IKeywordHybridSearchable` |
| V10 | `hybrid-search-rrf.md` documents raw RRF scores |
| V20 | `ResolveDenseMetricType` reads SG/definition schema; L2 SG integration test added |

## P2 closures

| ID | Resolution |
|----|------------|
| V11 | Task 0.3.1 CPM version → 10.9.0 |
| V12 | Verification matrix → ≥40 Facts |
| V13–V15 | Status banners corrected (1.11 shipped, 2.4 partial, 2.6 planned) |
| V16–V17 | `citation-schema.md` token-v1 + `[ZVecField]` attributes |
| V18 | This report + `latest.md` |
| V19 | `S-TEXTCHUNKER` reopened → Fixed after plan alignment |

## Remaining design gaps (do not block unrelated WRITE)

- **D-3** `IRagMigrationManager` — named in tasks/exceptions; no implementation story (blocks migration WRITE only).
- **D-1** `IRagEvaluator` — Story 2.8 tasked, not implemented.

## Harness hardening shipped

- `spec-lock.md` §2 → `docs/**`; §8 story-ID walk; §9 registry Fixed gate
- `.agents/gaps/plan_alignment_check.py` in CI (`gap-detection.yml`, `quality-gate.yml`)
- `tests/ZVec.Rag.AotTestApp` + `rag-aot-smoke` CI job (Story 2.7.1)

## Test honesty sprint

- Overclaim tests tightened (`HonorsCustomRrfK`, `HonorsAdditionalProperty`, `IsNullComparison`, SSE cancel, backpressure)
- Added: `DuplicateMode.Append`, `CitationOrder.ScoreDescending`, `AskAsync` history, L2 SG score path

## write_allowed rationale

All P1 spec contradictions and the SG Cosine bug are closed. Open `D-*` items are tasked/deferred and block only their epics.
