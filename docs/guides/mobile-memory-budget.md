# Mobile Memory Budget & Quantization Guide

## Overview

When running embedded RAG on mobile devices via MAUI Blazor Hybrid (iOS and Android), memory constraint management is critical. Unlike server environments, mobile operating systems impose strict per-app RAM limits (e.g. 1.5 GB on iOS devices; OS kills processes exceeding bounds).

This guide documents memory calculations, quantization options, and memory-mapped file (`mmap`) operational rules for mobile deployments.

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
| **INT8 (Recommended)** | 8-bit Integer | 1.0 | **76.8 MB** | 4.0x |
| **INT4** | 4-bit Packed Integer | 0.5 | **38.4 MB** | 8.0x |

---

## Operational Constraints on MAUI Blazor Hybrid

### 1. `EnableMmap = false` Operational Constraint
On MAUI Blazor Hybrid (Windows/iOS/Android), memory-mapped file I/O (`mmap`) must be disabled (`EnableMmap = false`) to ensure process stability across hybrid webview reloads. 

As a result, the active vector index is loaded into heap memory upon collection open.

### 2. Mobile Best Practice Recommendations

```csharp
// Recommended MAUI Mobile Initialization (MAUI Blazor Hybrid)
builder.Services.AddZVecVectorStore(options =>
{
    options.StoragePath = Path.Combine(FileSystem.AppDataDirectory, "mobile_rag.zvec");
    options.EnableMmap = false; // Required for MAUI Hybrid stability
    options.Quantization = ZVecQuantizationMode.Int8; // 4x memory reduction (76.8 MB for 100k 768-d vectors)
    options.MaxConcurrentNativeCalls = 4; // Bound native concurrency for mobile CPU thermal control
});
```

### 3. Recommended Corpus Bounds for Mobile RAG

- **Target Corpus Size:** \(\le 20,000\) chunks (approx. 15.3 MB footprint under INT8).
- **Embedded LLM Model:** Prefer 0.5B to 1.5B quantized GGUF models (e.g. Qwen 0.5B Q4_K_M ~350 MB) for on-device execution.
