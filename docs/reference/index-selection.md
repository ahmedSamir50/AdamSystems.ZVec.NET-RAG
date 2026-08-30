# Index Selection & Parameter Tuning Guide

## Overview

`ZVec.NET` supports multiple index algorithms (HNSW, Flat, IVF, Vamana, DiskANN). Selecting the correct index type and tuning its parameters is critical for balancing search recall, latency, and memory footprint in `ZVec.Rag`.

---

## Index Selection Decision Matrix

| Corpus Size | Recommended Index | Memory Footprint | Latency Profile | Best Use Case |
|---|---|---|---|---|
| **< 10,000 Chunks** | **Flat** (Exact Search) | Minimal | < 3.7 ms | Desktop apps, small document collections |
| **≤ 20,000 Chunks (mobile)** | **Flat** (Exact Search) | Minimal | < 3.7 ms | **Sample 03 default** — Flat index + shipped `Fp16` dtype; optional INT8 only if Recall@K ≥0.95 vs FP32 Flat baseline |
| **10k – 100k Chunks** | **HNSW** (Hierarchical Navigable Small World) | High (RAM) | < 1.0 ms | Production desktop/server RAG, default recommendation |
| **100k – 1M Chunks** | **IVF** (Inverted File Index) | Moderate | < 2.5 ms | Large corpus, mobile memory-constrained RAG |
| **> 1,000,000 Chunks** | **DiskANN** (Linux only) | Low (Disk I/O) | < 5.0 ms | Enterprise server clusters with high vector volume |

---

## Algorithm Parameter Tuning Guidelines

### 1. HNSW Parameters

```csharp
[ZVecVector(768, Metric = ZVecMetricType.Cosine, M = 16, EfConstruction = 200)]
public ReadOnlyMemory<float> Embedding { get; set; }
```

- **`M` (Max links per node):** Default `16` (range: 16–48). Higher `M` improves recall for high-dimensional embeddings (768-d+) at the cost of memory.
- **`EfConstruction`:** Default `200` (range: 200–400). Higher values increase build/ingest time but improve index graph connectivity.
- **`EfSearch`:** `ZVecRagOptions` default set to `100` (balanced). Engine raw default `300` provides ultra-high recall but increases latency 2–3x. Set `EfSearch = 50` for ultra-low latency or `efSearch = 300` for maximum recall.

### 3. Vector Quantization (`ZVecQuantizeType`)

```csharp
builder.Services.AddZVecVectorStore(opts =>
{
    opts.DefaultQuantizeType = ZVecQuantizeType.Int8; // store-level default for HNSW vectors
});

// Per-property override via VectorStore index metadata:
[VectorStoreVector(768, IndexKind = nameof(ZVecQuantizeType.Int8))]
public ReadOnlyMemory<float> Embedding { get; set; }

// FP16 storage when embedding property uses Half:
public ReadOnlyMemory<Half> Embedding { get; set; } // → VectorFp16 via ZVecVectorIndexResolver
```

| Type | Platform | Notes |
|---|---|---|
| `Undefined` (FP32) | All | Default; highest recall baseline |
| `Int8` | All (ARM-safe) | Optional for mobile; measure Recall@K before adopting |
| `Fp16` | All | Via `Half` embedding type; ARM-safe memory cut |
| `RaBitQ` | x86_64 / AVX2 only | **Not** for iOS/Android — desktop/server only |

Phase 4 Task 4.3.2 benchmarks Recall@K across FP32 vs FP16 vs INT8. Sample 03 uses Story 2.8 gate (≥0.95 vs FP32 Flat) before optional INT8.

### Changing Quantization on an Existing Collection

Changing `DefaultQuantizeType`, `IndexKind`, or `EmbeddingType` requires **delete + re-ingest** — not `EnsureSchema`. The embedder stamp manifest (Story 1.11) validates `QuantizeType` on open. See [Quantization & Index Rebuild Guide](../guides/quantization.md).

### 2. IVF Parameters

```csharp
var ivfParam = new ZVecIvfIndexParam
{
    CentroidsNum = 256, // sqrt(N) rule of thumb (256 centroids for ~65k vectors)
    Nprobe = 32         // Rule of thumb: CentroidsNum / 8 (default 8 is too low for RAG recall)
};
```

- **`CentroidsNum`:** Set to roughly \(\sqrt{N}\) where \(N\) is corpus vector count.
- **`Nprobe`:** Critical for recall. Native engine default `8` scans only ~3% of clusters, giving ~60-70% recall@10. For production RAG, set `Nprobe` to \(\sim \text{CentroidsNum} / 8\) (e.g. `32` for 256 centroids) to achieve >95% recall@10.
