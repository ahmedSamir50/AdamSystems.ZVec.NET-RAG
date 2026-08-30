# ZVec.Rag.Testing

Deterministic test fakes for `ZVec.Rag` pipelines — no cloud LLMs required in CI.

**Version:** `0.5.0-preview.1` · **TFMs:** `net8.0`, `net9.0`, `net10.0`

## Install

```bash
dotnet add package ZVec.Rag.Testing
```

Reference from test projects only (not production apps).

## Shipped today

- **`DeterministicEmbedder`** — hash-based `IEmbeddingGenerator<string, Embedding<float>>` (pipeline wiring tests)
- **`FakeChatClient`** — configurable streaming/non-streaming `IChatClient`
- **`SemanticTestEmbedder`** — token-overlap embedder that preserves lexical rank order for retrieval metric tests
- **`IRagEvaluator` / `DeterministicEvaluator`** — Recall@K, MRR, nDCG, and `RecallAtKLift` over ranked chunk ids
- **`IRagGenerationEvaluator` / `LlmJudgeGenerationEvaluator`** — optional Faithfulness / Context Precision via `IChatClient` (off in CI)

In-repo labeled **seed** fixtures live under `tests/ZVec.Rag.Tests/Fixtures/` (two queries today; not packed into this NuGet).

## Cross-navigation

| If you need… | Also install… |
|---|---|
| The RAG pipeline under test | **`ZVec.Rag`** |
| Vector store without RAG | **`ZVec.Extensions.VectorData`** |

## License

Apache-2.0
