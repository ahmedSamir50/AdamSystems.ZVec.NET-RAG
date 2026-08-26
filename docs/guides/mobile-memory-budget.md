# Mobile Memory Budget & Quantization Guide

## Overview

When running embedded RAG on mobile devices via MAUI Blazor Hybrid (iOS and Android), memory constraint management is critical. Unlike server environments, mobile operating systems impose strict per-app RAM limits (e.g. 1.5 GB on iOS devices; OS kills processes exceeding bounds).

This guide documents memory calculations, **vector** quantization options, and memory-mapped file (`mmap`) operational rules for mobile deployments.

> **Important:** Vector `ZVecQuantizeType` (INT8/FP16) reduces **embedding index** footprint. It is **not** LLM GGUF quantization. On-device LLamaSharp is **not** recommended for mobile Sample 03 — use a remote `IChatClient` or pre-generated answers.

---

## Memory Footprint Calculation Formula

The raw RAM footprint of a vector collection is determined by chunk count \(N\), vector dimension \(d\), and bytes per component \(b\):

$$
\text{MemoryBytes} = N \times d \times b
$$

| Quantization Type | Component Format | Bytes per Component (\(b\)) | 100,000 Chunks (768-d) | Footprint Reduction |
|---|---|---|---|---|
| **None (FP32)** | 32-bit Float | 4.0 | **307.2 MB** | 1.0x (Baseline) |
| **FP16** | 16-bit Half-Float | 2.0 | **153.6 MB** | 2.0x |
| **INT8** | 8-bit Integer | 1.0 | **76.8 MB** | 4.0x |

> **RaBitQ** (higher compression) requires x86_64/AVX2 and is **not** available on ARM mobile targets.

---

## Operational Constraints on MAUI Blazor Hybrid

### 1. Shipped read-only indexes: `EnableMmap = true` + `ReadOnly = true`

For **pre-built indexes shipped from desktop ingest** (Sample 03), enable memory-mapped I/O and open read-only. This keeps heap pressure low and matches the ZVec.NET MAUI sample pattern.

Indexes built **on-device** must use `ReadOnly = false` during ingest, then reopen read-only for query-only mode.

### 2. Mobile Best Practice Recommendations (Sample 03)

**Default:** Flat index (exact search, zero recall loss) for ≤20k chunks. Do **not** mandate HNSW+INT8.

```csharp
// Recommended MAUI Mobile Initialization (shipped read-only Flat index)
builder.Services.AddZVecVectorStore(options =>
{
    options.StoragePath = Path.Combine(FileSystem.AppDataDirectory, "mobile_rag.zvec");
    options.EnableMmap = true;   // mmap for shipped indexes
    options.ReadOnly = true;     // query-only on device
    // Flat index built on desktop — no DefaultQuantizeType required for ≤20k
    options.MaxConcurrentNativeCalls = 4;
});
```

**Optional INT8 HNSW** (memory reduction): only adopt if a **desktop** Recall@K check on the shipped fixture (Story 2.8 `IRagEvaluator`) stays **≥ 0.95 relative to FP32 Flat**. If INT8 fails, keep Flat or use Flat+FP16 (`EmbeddingType=Half`).

### 3. Recommended Corpus Bounds for Mobile RAG

- **Target Corpus Size:** \(\le 20{,}000\) chunks.
- **Index:** Flat (default for Sample 03); HNSW only if corpus grows past Flat comfort zone and recall is measured.
- **On-device LLM:** **Not recommended** for Sample 03. Use remote `IChatClient` or retrieval-only UX.
- **Vector vs LLM quant:** `DefaultQuantizeType` applies to the **vector store**. Do not conflate with GGUF Q4_K_M LLM weights.

See also: [Quantization & Index Rebuild Guide](quantization.md).
