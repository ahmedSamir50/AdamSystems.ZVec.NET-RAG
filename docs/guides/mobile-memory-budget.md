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

**Default:** Flat index (exact search) for ≤20k chunks. **Shipped dtype:** `ZVecQuantizeType.Fp16` (quantized storage — not FP32). Index type (Flat vs HNSW) is independent of storage dtype.

> **Never open a ZVec collection on the UI/main thread in MAUI.** Initialize the collection on a background thread during app startup and show a loading spinner in the Blazor WebView until `IZvecCollection<T>` is ready. This is an exception to the ingest `Task.Run` ban — collection open only.

```csharp
// Recommended MAUI Mobile Initialization (shipped read-only Flat + Fp16 index)
builder.Services.AddZVecVectorStore(options =>
{
    options.StoragePath = Path.Combine(FileSystem.AppDataDirectory, "mobile_rag.zvec");
    options.EnableMmap = true;   // mmap for shipped indexes
    options.ReadOnly = true;     // query-only on device
    options.DefaultQuantizeType = ZVecQuantizeType.Fp16; // shipped quantized dtype
    options.MaxConcurrentNativeCalls = 4;
});
```

**Optional INT8** (memory reduction): only adopt if a **desktop** Recall@K check on the shipped fixture (Story 2.8 `IRagEvaluator`) stays **≥ 0.95 relative to FP32 Flat**. If INT8 fails, keep Fp16 Flat.

### 3. Recommended Corpus Bounds for Mobile RAG

- **Target Corpus Size:** \(\le 20{,}000\) chunks.
- **Index:** Flat (default for Sample 03); HNSW only if corpus grows past Flat comfort zone and recall is measured.
- **On-device LLM:** **Not recommended** for Sample 03. Use remote `IChatClient` or retrieval-only UX.
- **Vector vs LLM quant:** `DefaultQuantizeType` applies to the **vector store**. Do not conflate with GGUF Q4_K_M LLM weights.

See also: [Quantization & Index Rebuild Guide](quantization.md).

---

## Measuring thinned `.ipa` / `.apk` (methodology)

1. Build the MAUI Blazor Hybrid app with release configuration and platform-specific publish (`dotnet publish -f net8.0-ios` / `net8.0-android`).
2. For iOS: export an `.ipa` and measure **thinned** size via App Store Connect / Transporter validation or Xcode Organizer (App Thinning report). Do not cite unsigned simulator build sizes as shipping numbers.
3. For Android: measure **download-size** from Play Console internal testing or `bundletool build-apks` + `get-size` on the generated `.aab`.
4. Record cold-start separately: time from process launch until first successful `RetrieveAsync` after `BackgroundCollectionOpener` completes (target &lt; 3s on mid-range Android — measure on hardware; not automated in this repo yet).

## Wi-Fi-only onboarding policy

When a cellular install cap would be exceeded:

1. **Quantize the index first** (`Fp16` shipped default; optional `Int8` only after desktop Recall@K gate).
2. **Then** apply distribution policy: Wi-Fi-only download of the pre-built index bundle, not embedding precision as the primary lever.
3. Document the cellular cap your org enforces; this repo does **not** publish signed `.ipa` megabyte figures (H-IPA-DEVICE).

On-Demand Resources and store-hosted index bundles are **post-v1** distribution options — no ODR code ships in Sample 03.
