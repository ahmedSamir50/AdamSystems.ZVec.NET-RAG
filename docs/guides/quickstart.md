# Quickstart (RAG in 60 Seconds)

Scaffold and run a working local-first RAG application in 60 seconds with **both** Document Ingestion and Hybrid Retrieval + Generation.

---

## 1. Install Project Template

```bash
dotnet new install ZVec.Rag.Template
```

---

## 2. Scaffold Application

```bash
dotnet new zvec-rag -n MyLocalRagApp --llm fake --embedder fake
```

---

## 3. Honest 20-Line Application Code (Ingestion + SSE Chat)

```csharp
using ZVec.Rag.Abstractions;
using ZVec.Rag.Streaming;
using ZVec.Rag.Testing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddZVecRag(opts =>
{
    opts.StoragePath = "./rag.zvec";
    opts.Embedder = new DeterministicEmbedder();
    // Test double for CI and first run. Concatenates tokens; does not call a model. Replace with your IChatClient (Story 4.1).
    opts.Chat = new FakeChatClient("Hello", " from ZVec.Rag");
})
.AddTokenChunker(maxTokens: 512, overlapTokens: 64)
.AddMarkdownChunker();

var app = builder.Build();

// 1. Document Ingestion Endpoint (text / markdown auto-chunked)
app.MapPost("/ingest", async (string text, string docId, IRagPipeline rag) =>
{
    await rag.IngestTextAsync(text, documentId: docId);
    return Results.Ok($"Indexed document {docId}");
});

// 2. Real-time unbuffered SSE streaming chat (?question=...)
// Links HttpContext.RequestAborted to AskAsync so client disconnect cancels generation.
app.MapRagSseEndpoint("/chat");

app.Run();
```

Replace `FakeChatClient` and `DeterministicEmbedder` with your `IChatClient` and `IEmbeddingGenerator` when you add a real LLM (Story 4.1 recipe packages).

---

## 4. Advanced Multi-Format Ingestion (PDF via Optional Package)

Core `ZVec.Rag` ingests **text and markdown** only. For PDF documents, add the optional `ZVec.Rag.Pdf` package.

```csharp
builder.Services.AddZVecRag(opts => { /* ... */ })
    .AddTokenChunker()
    .AddZVecRagPdf();

app.MapPost("/ingest/pdf", async (IFormFile file, IRagPipeline rag) =>
{
    await rag.IngestDocumentAsync(
        file.OpenReadStream(),
        documentId: file.FileName,
        contentType: "application/pdf",
        options: new IngestOptions { OnDuplicate = DuplicateMode.Replace });
    return Results.Ok("PDF ingested");
});
```

This sample extracts PDF text only. Table-cell QA is post-v1 (Epic 8.7 / D-7).

For markdown with explicit heading chunking, register `AddMarkdownChunker()` — `IngestDocumentAsync` selects it automatically for `text/markdown` content types.
