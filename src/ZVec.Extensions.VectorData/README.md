# ZVec.Extensions.VectorData

Official [`Microsoft.Extensions.VectorData`](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-vector-data) connector for [ZVec.NET](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET) — embedded, local-first vector storage with hybrid dense + FTS search.

**Version:** `1.0.0-preview.1` · **TFMs:** `net8.0`, `net9.0`, `net10.0`

## Install

```bash
dotnet add package ZVec.Extensions.VectorData
```

For zero-reflection Native AOT record mappers, also reference `ZVec.Extensions.VectorData.SourceGenerator` as an analyzer.

## Quick start

```csharp
services.AddZVecVectorStore(options =>
{
    options.StoragePath = "./vectors.zvec";
    options.MaxConcurrentNativeCalls = Environment.ProcessorCount;
});
```

## Cross-navigation

| If you need… | Also install… |
|---|---|
| Full RAG orchestration (ingest, retrieve, cite, SSE) | **`ZVec.Rag`** |
| Unit test fakes without LLMs | **`ZVec.Rag.Testing`** (with `ZVec.Rag`) |
| Roslyn AOT mappers for `[VectorStoreRecord]` POCOs | **`ZVec.Extensions.VectorData.SourceGenerator`** |

## License

Apache-2.0
