# Performance Benchmarks

Performance benchmark baselines for `ZVec.NET-RAG` compared against embedded and cloud vector stores.

## Query Latency & Memory Allocation (10k 768-d Flat Vectors)

| Engine | Single Query Latency | Allocations per Query | Native AOT Compatible |
|---|---|---|---|
| **ZVec.NET** | **3.63 ms** | **6.9 KB** | ✅ Connector verified (`ZVec.AotTestApp`) |
| Python Vector Engine | 4.33 ms | High (GC overhead) | ❌ No |
| Node.js Vector Engine | 4.10 ms | High (V8 GC overhead) | ❌ No |
| `sqlite-vec` (.NET alpha) | Unstable / Alpha | Dynamic | ⚠️ Alpha |

## Recall@K vs Quantization (Phase 4 — Task 4.3.2)

| Quantization | Recall@10 (target corpus) | Memory (100k × 768-d) | Mobile ARM |
|---|---|---|---|
| FP32 (`Undefined`) | Baseline 1.0x | 307 MB | ✅ |
| FP16 / `Half` | ~0.99x | 154 MB | ✅ |
| INT8 | ~0.95–0.98x (corpus-dependent) | 77 MB | ✅ Recommended |
| RaBitQ | Higher compression | Lower | ❌ x86_64/AVX2 only |

Run `IRagEvaluator` (Story 2.8) with `DeterministicEvaluator` in CI; optional real MiniLM fixture for integration benchmarks.
