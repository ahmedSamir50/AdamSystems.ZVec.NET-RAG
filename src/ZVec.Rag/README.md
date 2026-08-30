# ZVec.Rag

Batteries-included local-first RAG for .NET on `Microsoft.Extensions.AI` and `Microsoft.Extensions.VectorData`.

**Version:** `1.0.0-preview.1` · **TFMs:** `net8.0`, `net9.0`, `net10.0`

## Install

```bash
dotnet add package ZVec.Rag
```

Optional PDF ingestion:

```bash
dotnet add package ZVec.Rag.Pdf
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

For PDF: add `.AddZVecRagPdf()` after `AddZVecRag`.

## Cross-navigation

| If you need… | Also install… |
|---|---|
| Pure vector storage (Semantic Kernel / Agent Framework) | **`ZVec.Extensions.VectorData`** |
| Unit test fakes (`DeterministicEmbedder`, `FakeChatClient`, `IRagEvaluator`) | **`ZVec.Rag.Testing`** |
| PDF text extraction | **`ZVec.Rag.Pdf`** (not trim-safe; not in AOT smoke) |
| Local LLM adapters (LLamaSharp, ONNX) | Planned **Story 4.1** — not in this package yet |
| `dotnet new zvec-rag` template | **`ZVec.Rag.Template`** — `dotnet new install ZVec.Rag.Template` |

## License

Apache-2.0
