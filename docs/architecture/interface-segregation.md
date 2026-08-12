# RAG Pipeline Interface Segregation (ISP)

> **Status:** Planned for Phase 2 (Story 2.1 — IRagIngestor, IRagRetriever, IRagGenerator Split Interfaces & RagPipeline Facade).
> The interface segregation design described here is the target architecture for Phase 2.

The `ZVec.Rag` framework enforces strict **Interface Segregation Principle (ISP)** compliance to eliminate God interfaces and allow application components to depend strictly on the capabilities they require.

---

## 🏗️ Interface Topology

Rather than bundling document ingestion, context retrieval, and LLM text generation into a single monolithic type, `ZVec.Rag` decomposes pipeline capabilities into three single-responsibility interfaces and one composite facade:

```
                  ┌──────────────────────┐
                  │    IRagIngestor      │
                  │ (IngestTextAsync,    │
                  │  IngestDocumentAsync)│
                  └──────────┬───────────┘
                             │
     ┌───────────────────────┼───────────────────────┐
     │                       │                       │
┌────▼─────────────────┐  ┌──▼──────────────────┐  ┌─▼────────────────────┐
│    IRagRetriever     │  │    IRagGenerator    │  │    IRagPipeline      │
│ (RetrieveAsync)      │  │ (AskAsync)          │  │ (Composite Facade)   │
└──────────────────────┘  └─────────────────────┘  └──────────────────────┘
```

---

## 📜 Interface Definitions

### 1. `IRagIngestor` — Document Ingestion & Chunking
Responsible for reading documents, chunking text, generating vector embeddings, and writing records to the vector store.

```csharp
public interface IRagIngestor
{
    ValueTask<IngestionResult> IngestTextAsync(
        string text, 
        string documentId, 
        IngestOptions? options = null, 
        CancellationToken ct = default);

    ValueTask<IngestionResult> IngestDocumentAsync(
        Stream documentStream, 
        string documentId, 
        string contentType, 
        IngestOptions? options = null, 
        CancellationToken ct = default);
}
```

### 2. `IRagRetriever` — Hybrid Vector & FTS Search
Responsible for querying the vector store using dense vector similarity, full-text search (FTS), and native Reciprocal Rank Fusion (RRF).

```csharp
public interface IRagRetriever
{
    Task<IReadOnlyList<Citation>> RetrieveAsync(
        string query, 
        HybridSearchOptions? options = null, 
        CancellationToken ct = default);
}
```

### 3. `IRagGenerator` — LLM Generation & Token Streaming
Responsible for retrieving context, assembling prompt windows, querying `IChatClient`, and streaming SSE tokens with citations.

```csharp
public interface IRagGenerator
{
    IAsyncEnumerable<RagChunk> AskAsync(
        string question, 
        IList<ChatMessage>? history = null, 
        bool streamCitations = true, 
        CancellationToken ct = default);
}
```

### 4. `IRagPipeline` — Composite Facade
Implements `IRagIngestor`, `IRagRetriever`, and `IRagGenerator` for single-service injection convenience in small applications.

```csharp
public interface IRagPipeline : IRagIngestor, IRagRetriever, IRagGenerator
{
}
```

---

## 🎯 Dependency Injection Best Practices

Inject only the specific interface required by each component:

```csharp
// Background worker service — needs ONLY ingestion capability
public sealed class DocumentIngestionWorker(IRagIngestor ingestor) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await ingestor.IngestTextAsync("Policy text...", "policy-01", ct: ct);
    }
}

// Search API controller — needs ONLY retrieval capability
[ApiController]
[Route("api/search")]
public sealed class SearchController(IRagRetriever retriever) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct) =>
        Ok(await retriever.RetrieveAsync(q, ct: ct));
}
```
