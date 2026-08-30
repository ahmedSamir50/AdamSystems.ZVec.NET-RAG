# Performance Benchmarks

Performance benchmark baselines for `ZVec.NET-RAG` compared against embedded and cloud vector stores.

## Query Latency & Memory Allocation (10k 768-d Flat Vectors)

| Engine | Single Query Latency | Allocations per Query | Native AOT Compatible |
|---|---|---|---|
| **ZVec.NET** | **3.63 ms** | **6.9 KB** | ✅ Connector verified (`ZVec.AotTestApp`) |
| Python Vector Engine | 4.33 ms | High (GC overhead) | ❌ No |
| Node.js Vector Engine | 4.10 ms | High (V8 GC overhead) | ❌ No |
| `sqlite-vec` (.NET alpha) | Unstable / Alpha | Dynamic | ⚠️ Alpha |

The **6.9 KB** allocation figure is the engine baseline for 10k × 768-d flat vectors. The local `ZVec.Rag.Benchmarks` project (`QueryAllocationBenchmarks`) measures retrieval over a **256-chunk** deterministic corpus — use it for regression checks, not as a substitute for the 10k engine number.

## Recall@K vs Quantization (Story 2.8 / 4.3.2 fixture)

Measured on the in-repo seed fixture (`tests/ZVec.Rag.Tests/Fixtures/`, `SemanticTestEmbedder`, first `qa.jsonl` query, `Recall@5`):

| Quantization | Recall@5 (fixture) | Memory (100k × 768-d) | Mobile ARM |
|---|---|---|---|
| FP32 (`Undefined`) | **1.000** | 307 MB | ✅ |
| FP16 / `Half` | **1.000** | 154 MB | ✅ |
| INT8 | **1.000** | 77 MB | ✅ Recommended |

Fixture is `SemanticTestEmbedder` on Story 2.8 seed; not MiniLM SOTA; not README marketing. CI gates FP16 ≥ 95% of FP32 when baseline &gt; 0 (`Sample03RecallGateTests`, `QuantizeDtypeRecallTests`); INT8 ratio below 0.95 is informational only.

`IRagEvaluator` / `DeterministicEvaluator` / `SemanticTestEmbedder` run in unit and integration tests. There is **no** public Recall@K CI job or marketing number. Optional real MiniLM fixture for local integration benchmarks stays gitignored.

## Local BenchmarkDotNet project

`tests/ZVec.Rag.Benchmarks` (`QueryAllocationBenchmarks`, `[MemoryDiagnoser]`) ingests 256 chunks via `DeterministicEmbedder` then benchmarks `RetrieveAsync`. Run locally:

```bash
dotnet run -c Release --project tests/ZVec.Rag.Benchmarks/ZVec.Rag.Benchmarks.csproj
```

Not part of the GitHub Actions quality gate.
