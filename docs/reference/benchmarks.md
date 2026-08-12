# Performance Benchmarks

Performance benchmark baselines for `ZVec.NET-RAG` compared against embedded and cloud vector stores.

## Query Latency & Memory Allocation (10k 768-d Flat Vectors)

| Engine | Single Query Latency | Allocations per Query | Native AOT Compatible |
|---|---|---|---|
| **ZVec.NET** | **3.63 ms** | **6.9 KB** | ✅ Yes |
| Python Vector Engine | 4.33 ms | High (GC overhead) | ❌ No |
| Node.js Vector Engine | 4.10 ms | High (V8 GC overhead) | ❌ No |
| `sqlite-vec` (.NET alpha) | Unstable / Alpha | Dynamic | ⚠️ Alpha |
