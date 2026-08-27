# ZVec.Rag

Batteries-included local-first RAG for .NET on `Microsoft.Extensions.AI` and `Microsoft.Extensions.VectorData`.

**Version:** `0.5.0-preview.1` · **TFMs:** `net8.0`, `net9.0`, `net10.0`

## Install

```bash
dotnet add package ZVec.Rag
```

## Quick start

```csharp
services.AddZVecRag(opts =>
{
    opts.StoragePath = "./rag.zvec";
    opts.Embedder = myEmbeddingGenerator;
    opts.Chat = myChatClient;
})
.AddTokenChunker();
```

## Cross-navigation

| If you need… | Also install… |
|---|---|
| Pure vector storage (Semantic Kernel / Agent Framework) | **`ZVec.Extensions.VectorData`** |
| Unit test fakes (`DeterministicEmbedder`, `FakeChatClient`) | **`ZVec.Rag.Testing`** |
| Local LLM adapters (LLamaSharp, ONNX) | Planned **Story 4.1** — not in this package yet |
| `dotnet new rag` template | Planned **Story 3.1** |

## License

Apache-2.0
