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
dotnet new rag -n MyLocalRagApp --llm ollama --embedder ollama
```

---

## 3. Honest 20-Line Application Code (Ingestion + SSE Chat)

```csharp
using Microsoft.Extensions.AI;
using ZVec.Rag;
using ZVec.Rag.Streaming;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddZVecRag(opts => {
    opts.StoragePath = "./rag.zvec";
    opts.Embedder = ollama.Embeddings(model: "nomic-embed-text");
    opts.Chat = ollama.Chat(model: "llama3.2");
})
.AddTokenChunker(maxTokens: 512, overlapTokens: 64)
.AddMarkdownChunker();

var app = builder.Build();

// 1. Document Ingestion Endpoint (text / markdown auto-chunked)
app.MapPost("/ingest", async (string text, string docId, IRagPipeline rag) => {
    await rag.IngestTextAsync(text, documentId: docId);
    return Results.Ok($"Indexed document {docId}");
});

// 2. Real-time unbuffered SSE streaming chat (?question=...)
// Links HttpContext.RequestAborted to AskAsync so client disconnect cancels generation.
app.MapRagSseEndpoint("/chat");

app.Run();
```

---

## 4. Advanced Multi-Format Ingestion (PDF via Optional Package)

Core `ZVec.Rag` ingests **text and markdown** only. For PDF documents, add the optional `ZVec.Rag.Pdf` package.

> **Status:** Planned — `ZVec.Rag.Pdf` and `PdfDocumentReader` ship in Sample 02 (`02-local-first-pdf-chat`). Not in core or the Story 2.7 AOT harness.

```csharp
// Planned: optional ZVec.Rag.Pdf — not in core AOT path
app.MapPost("/ingest/pdf", async (IFormFile file, IRagPipeline rag) => {
    await rag.IngestDocumentAsync(
        file.OpenReadStream(),
        documentId: file.FileName,
        contentType: "application/pdf",
        options: new IngestOptions { OnDuplicate = DuplicateMode.Replace });
    return Results.Ok("PDF ingested");
});
```

For markdown with explicit heading chunking, register `AddMarkdownChunker()` — `IngestDocumentAsync` selects it automatically for `text/markdown` content types.
