<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/assets/zvec-rag-logo-dark.png">
  <img src="docs/assets/zvec-rag-logo-light.png" width="360" alt="ZVec.Rag">
</picture>

> **Local-first RAG for .NET. No cloud. No Python. No kidding.**

`ZVec.Rag` is an embedded, local-first Retrieval-Augmented Generation (RAG) stack on [ZVec.NET](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET) plus `Microsoft.Extensions.VectorData` and `Microsoft.Extensions.AI`. Pipeline, templates, local recipe packages, and stage telemetry are shipped; wiki at [`docs/`](docs/).

---

## Start here

1. **Template (no model files):**
   ```bash
   dotnet new install ZVec.Rag.Template
   dotnet new zvec-rag -n MyApp --llm fake --embedder fake
   ```
2. **60-second console demo:**
   ```bash
   dotnet run --project samples/01-rag-your-docs/ZVec.Rag.Sample01.csproj
   ```
3. **Pick a host:**
   - **Console** — `zvec-rag` (above) or Sample 01
   - **ASP.NET + SSE** — `dotnet new zvec-rag-aspnet -n MyApi` then `MapRagSseEndpoint("/chat")`
   - **MAUI Blazor Hybrid** — `dotnet new zvec-rag-maui -n MyPhone` (retrieve + cite; **no** on-device LLamaSharp)

---

## Wire a real model

| Goal | What to use | What not to expect |
|---|---|---|
| CI / first run | `FakeChatClient` + `DeterministicEmbedder` | Not a real LLM |
| Local GGUF chat | `ZVec.Rag.LLamaSharp`, `AddZVecRagLLamaSharp`, env `ZVEC_LLAMA_MODEL`; optional `ZVEC_LLAMA_EMBED=1` | Not in AOT smoke; desktop/server |
| ONNX 768-d text embed | `ZVec.Rag.ONNX`, `OnnxEmbeddingModelKind.EmbeddingGemma`, env `ZVEC_ONNX_MODEL` | Without env, template falls back to Deterministic |
| CLIP 512-d images | `ZVecRagMultimodalRecordV1` + `ClipText`; see [`docs/guides/multimodal-rag.md`](docs/guides/multimodal-rag.md) | Sample **05** planned; default `ZVecRagRecordV1` is 768-d text |
| Ollama / Azure / OpenAI | `Microsoft.Extensions.AI` clients directly in your host | **No** `ZVec.Rag.Ollama` package |

**Sample 04** (`samples/04-airgapped-enterprise-rag`) switches to LLamaSharp when `ZVEC_LLAMA_MODEL` points at a GGUF file; otherwise Fake/Deterministic.

**OpenTelemetry:** `ZVec.Rag` emits `ActivitySource` / `Meter` named `ZVec.Rag`. Host wiring: [`docs/architecture/di-composition.md`](docs/architecture/di-composition.md) (`AddSource("ZVec.Rag")`, `AddMeter("ZVec.Rag")`, your OTLP exporter).

---

## ASP.NET quickstart (Fake for smoke)

```csharp
using ZVec.Rag;
using ZVec.Rag.Streaming;
using ZVec.Rag.Testing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddZVecRag(opts => {
    opts.StoragePath = "./rag.zvec";
    opts.Embedder = new DeterministicEmbedder();
    opts.Chat = new FakeChatClient("Hello from ZVec.Rag");
})
.AddTokenChunker(maxTokens: 512, overlapTokens: 64);

// Real GGUF (desktop/server; not AOT-safe):
// builder.Services.AddZVecRagLLamaSharp(o =>
//     o.ModelPath = Environment.GetEnvironmentVariable("ZVEC_LLAMA_MODEL")!);

// Real ONNX 768-d embedder:
// builder.Services.AddZVecRagOnnxEmbedder(o => {
//     o.ModelPath = Environment.GetEnvironmentVariable("ZVEC_ONNX_MODEL")!;
//     o.ModelKind = OnnxEmbeddingModelKind.EmbeddingGemma;
//     o.Dimensions = ZVecRagRecordV1.DefaultDimensions;
// });

var app = builder.Build();
app.MapPost("/ingest", async (string text, string docId, IRagIngestor ingestor) => {
    await ingestor.IngestTextAsync(text, documentId: docId);
    return Results.Ok();
});
app.MapRagSseEndpoint("/chat");
app.Run();
```

---

## What v1 is / is not

**v1 is:** local-first **Naive RAG** — hybrid dense+FTS+RRF retrieve, token-budget context pack, one generate call — for pointed questions over text/markdown you ingest in-process.

**v1 is not:** layout-aware PDF table QA, multi-document research, auto metadata filters, production alerting/caches (post-v1 Epic 8.9), or a packaged Ollama adapter.

We do **not** publish Recall@K marketing here. Use `IRagEvaluator` / `DeterministicEvaluator` in tests; optional local runs stay gitignored.

**Native AOT:** `ZVec.Extensions.VectorData` and core `ZVec.Rag` pipeline are verified via `ZVec.Rag.AotTestApp` CI smoke (linux-x64, win-x64, osx-x64). **Not** in AOT smoke: PDF, SSE helpers, `ZVec.Rag.LLamaSharp`, `ZVec.Rag.ONNX`.

---

## Packages

| Package | Version | Role |
|---|---|---|
| **`ZVec.Extensions.VectorData`** | `1.0.0-preview.1` | M.E.VectorData connector for ZVec.NET |
| **`ZVec.Rag`** | `1.0.0-preview.1` | RAG orchestration (`IRagIngestor`, `IRagRetriever`, `IRagGenerator`, citations, SSE) |
| **`ZVec.Rag.Testing`** | `1.0.0-preview.1` | `DeterministicEmbedder`, `FakeChatClient`, evaluators |
| **`ZVec.Rag.Pdf`** | `1.0.0-preview.1` | Optional PdfPig text extract |
| **`ZVec.Rag.Template`** | `1.0.0-preview.1` | `dotnet new zvec-rag` / `zvec-rag-aspnet` / `zvec-rag-maui` |
| **`ZVec.Rag.LLamaSharp`** | `1.0.0-preview.1` | Local GGUF `IChatClient` / embed adapter |
| **`ZVec.Rag.ONNX`** | `1.0.0-preview.1` | ONNX Runtime embedder + multimodal record |
| **`ZVec.Extensions.VectorData.SourceGenerator`** | `1.0.0-preview.1` | AOT record mappers |
| **`ZVec.Extensions.VectorData.Analyzers`** | `1.0.0-preview.1` | `ZVEC001` / `ZVEC002` analyzers |

Package READMEs under `src/*/README.md`. Architecture: [`docs/architecture/rag-pipeline.md`](docs/architecture/rag-pipeline.md).

---

## Ingestion (short)

Core ships text/markdown readers and `IZVecTextChunker` (`AddTokenChunker`, `AddMarkdownChunker`, `AddSentenceChunker`). PDF via optional `ZVec.Rag.Pdf`. Bounded in-process channel queue (capacity 1024).

**Re-ingest from scratch** when you change embedder model, dimensions, quantize mode, chunker, or `GenerateSummaries` — delete storage or use a new `StoragePath`.

Optional **section summaries** (`IngestOptions.GenerateSummaries`, default **off**): second collection + parent boost at query time. See [`docs/architecture/rag-pipeline.md`](docs/architecture/rag-pipeline.md).

---

## Samples

| Sample | Path |
|---|---|
| 01 Console 60s | `samples/01-rag-your-docs/` |
| 02 PDF + SSE (EN/AR) | `samples/02-local-first-pdf-chat/` |
| 03 MAUI offline retrieve | `samples/03-offline-phone-rag/` |
| 04 Air-gapped ASP.NET | `samples/04-airgapped-enterprise-rag/` |
| 05 Multimodal CLIP | **Planned** |
| 06 Aspire dashboard | **Planned** |

---

## License

Apache-2.0 — see [LICENSE](LICENSE).
