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

## 3. Honest 20-Line Application Code (Ingestion + Chat)

```csharp
using Microsoft.Extensions.AI;
using ZVec.Rag;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddZVecRag(opts => {
    opts.StoragePath = "./rag.zvec";
    opts.Embedder = ollama.Embeddings(model: "nomic-embed-text");
    opts.Chat = ollama.Chat(model: "llama3.2");
});

var app = builder.Build();

// 1. Document Ingestion Endpoint (Text / Markdown auto-chunked by TokenTextChunker)
app.MapPost("/ingest", async (string text, string docId, IRagPipeline rag) => {
    await rag.IngestTextAsync(text, documentId: docId);
    return Results.Ok($"Indexed document {docId}");
});

// 2. Hybrid Search + LLM Generation Endpoint
app.MapPost("/chat", async (string question, IRagPipeline rag, CancellationToken ct) => {
    await foreach (var chunk in rag.AskAsync(question, streamCitations: true, ct))
        await Response.WriteAsync(chunk.Text, ct);
});

app.Run();
```

---

## 4. Advanced Multi-Format Ingestion (PDF via Optional Package)

Core `ZVec.Rag` ingests **text and markdown** only. For PDF documents, add the optional `ZVec.Rag.Pdf` package and configure an explicit `IRagDocumentReader`:

```csharp
// Advanced Ingestion with PDF Reader (optional ZVec.Rag.Pdf) & Markdown Heading Chunking
app.MapPost("/ingest/pdf", async (IFormFile file, IRagPipeline rag) => {
    await rag.IngestAsync(file.OpenReadStream(), options => {
        options.DocumentId = file.FileName;
        options.Reader = new PdfDocumentReader(); // ZVec.Rag.Pdf — not in core AOT path
        options.Chunker = TextChunker.ByMarkdownHeadings(maxTokens: 512, overlap: 50);
    });
    return Results.Ok("PDF ingested with heading-aware chunking");
});
```

> Sample 02 (`02-local-first-pdf-chat`) references `ZVec.Rag.Pdf`. The Story 2.7 AOT harness does **not**.

