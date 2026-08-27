# ZVec.Rag.Testing

Deterministic test fakes for `ZVec.Rag` pipelines — no cloud LLMs required in CI.

**Version:** `0.5.0-preview.1` · **TFMs:** `net8.0`, `net9.0`, `net10.0`

## Install

```bash
dotnet add package ZVec.Rag.Testing
```

Reference from test projects only (not production apps).

## Shipped today

- **`DeterministicEmbedder`** — hash-based `IEmbeddingGenerator<string, Embedding<float>>`
- **`FakeChatClient`** — configurable streaming/non-streaming `IChatClient`

`SemanticTestEmbedder` and `IRagEvaluator` ship in **Story 2.8**.

## Cross-navigation

| If you need… | Also install… |
|---|---|
| The RAG pipeline under test | **`ZVec.Rag`** |
| Vector store without RAG | **`ZVec.Extensions.VectorData`** |

## License

Apache-2.0
