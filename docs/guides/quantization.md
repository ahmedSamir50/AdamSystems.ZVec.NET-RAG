# Vector Quantization & Index Rebuild Guide

## Overview

`ZVec.Extensions.VectorData` supports vector index quantization via `ZVecQuantizeType` on HNSW/Flat/IVF indexes. This guide covers when to use quantization and what happens when you change settings on an existing collection.

> **Scalar INT8 vs RaBitQ:** `ZVecQuantizeType.Int8` is scalar index quantization (ARM-safe). `RaBitQ` requires x86_64/AVX2 and is **not** for iOS/Android.

---

## Configuration

```csharp
builder.Services.AddZVecVectorStore(opts =>
{
    opts.ModelId = "nomic-embed-text";
    opts.DefaultQuantizeType = ZVecQuantizeType.Int8; // store-level HNSW default
});

// Per-property override:
[VectorStoreVector(768, IndexKind = nameof(ZVecQuantizeType.Int8))]
public ReadOnlyMemory<float> Embedding { get; set; }

// FP16 storage via Half embedding type:
public ReadOnlyMemory<Half> Embedding { get; set; } // → VectorFp16
```

---

## Mobile Sample 03 Policy

| Setting | Default | Gate |
|---|---|---|
| Index type | **Flat** (≤20k chunks) | Exact recall, fast on mobile |
| Quantization | None (Flat FP32) | — |
| Optional INT8 HNSW | Only if measured | Recall@K ≥ **0.95** vs FP32 Flat on desktop fixture |
| Fallback if INT8 fails | Flat or Flat+FP16 | FP16 is ARM NEON-safe |

Do **not** blindly ship INT8 for the mobile demo. Run `IRagEvaluator` (Story 2.8) on the shipped fixture before locking config.

---

## Changing Quantization or Embedding Type (Rebuild Required)

Native ZVec `EnsureSchema` is **additive** for nullable numeric columns only. Changing any of the following on an existing collection requires **delete + re-ingest** (or `IRagMigrationManager` shadow rebuild):

- `DefaultQuantizeType` or per-property `IndexKind`
- `EmbeddingType` (`float` vs `Half`)
- Vector dimensions or embedder model (`ModelId`)

The embedder stamp manifest (`zvec_index_manifest.json`, Story 1.11) records `ModelId`, `Dimensions`, `QuantizeType`, and storage dtype. A mismatch throws `ZVecEmbedderMismatchException` with expected vs actual values and the storage path.

**There is no in-place HNSW requantize.** Do not reopen connector Stories 1.3/1.6 for runtime detection — the stamp is the single source of truth.

---

## Recall Measurement

- **Sample 03 gate:** Story 2.8 `IRagEvaluator` on desktop before shipping mobile index.
- **Full benchmark:** Phase 4 Task 4.3.2 — FP32 vs FP16 vs INT8 Recall@K on fixed fixture.

See also: [Index Selection Guide](../reference/index-selection.md), [Migration from InMemory](migration-from-inmemory.md).
