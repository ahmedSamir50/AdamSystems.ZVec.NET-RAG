# RAG Pipeline & Document Ingestion Architecture

`ZVec.Rag` provides the `IRagPipeline` and `IRagIngestor` orchestrators built on top of Microsoft AI ecosystem primitives:

> **Status:** Planned for Phase 2 (Stories 2.1, 2.2, 2.3 — RAG Pipeline, Ingestion, SSE)
```
┌─────────────────────────┐    ┌─────────────────────────┐    ┌─────────────────────────┐    ┌─────────────────────────┐
│   1. Document Reader    │ -> │    2. Text Chunker      │ -> │  3. Vector Embedder     │ -> │  4. Persistent Store    │
│  (PDF / HTML / MD / TXT │    │ (Token / Markdown AST / │    │ (IEmbeddingGenerator<   │    │(ZVec.VectorData +       │
│    / JSON Stream)       │    │  Sentence / Sliding)    │    │    string, Embedding>)  │    │     ZVec FTS Index)     │
└─────────────────────────┘    └─────────────────────────┘    └─────────────────────────┘    └─────────────────────────┘
```

---

## 1. Document Ingestion Architecture (`IRagIngestor`)

Ingestion is transparently divided into four distinct, pluggable stages aligned with `Microsoft.Extensions.DataIngestion`:

1. **Document Readers (`IDocumentReader`)**:
   - `PlainTextDocumentReader` (Default): Fast UTF-8 stream reader for plain text and Markdown.
   - `PdfDocumentReader`: Pluggable reader for extracting structured text and layout metadata from PDF documents (via optional `ZVec.Rag.Pdf` package).
   - `HtmlDocumentReader`: DOM stripper for converting web pages into clean content streams.
2. **Text Chunkers (`ITextChunker`)**:
   - `TokenTextChunker` (Default): Splits text strictly on token boundaries using `Microsoft.ML.Tokenizers` (e.g. 512 tokens with 64-token overlap).
   - `MarkdownHeadingChunker`: AST-aware chunker preserving section titles (`# H1`, `## H2`) attached as metadata to child paragraphs.
   - `SentenceTextChunker`: Prevents splitting mid-sentence for high-precision semantic search.
3. **Embedding Generation & Tokenizer Auto-Coupling**: Vectorizes chunks using `Microsoft.Extensions.AI` `IEmbeddingGenerator<string, Embedding<float>>`. The chunker's tokenizer is automatically aligned with the embedder model (SentencePiece for `nomic-embed-text`, Tiktoken for OpenAI, WordPiece for BERT). Standard tokenizer model files are bundled as embedded resources in `ZVec.Rag` for 100% offline air-gapped execution.
4. **Deduplication & Hybrid Persistence (`IngestOptions`)**: Supports `OnDuplicate = Replace | Append | Skip`. `Replace` performs filter-based chunk deletion (`SourceDoc == documentId`) before writing records to `ZVec.Extensions.VectorData` (`IVectorStore`) backing dense vector index + embedded FTS index.

---

## 2. Tokenizer Architecture: `Microsoft.ML.Tokenizers` & `tryAGI/Tiktoken`

Tokenization is critical for token-aware chunking and LLM prompt context budgeting.

- **Primary Tokenizer Engine (`Microsoft.ML.Tokenizers`)**:
  - Official Microsoft zero-allocation tokenizer engine.
  - Supports **Tiktoken BPE** (`cl100k_base`, `o200k_base` for OpenAI), **SentencePiece** (LLaMA 3, Nomic Embed, Mistral), and **WordPiece** (BERT, MiniLM).
  - Fully compatible with Native AOT trimming (`net8.0`/`net9.0`/`net10.0`).
- **Pluggable BPE Adapter (`tryAGI/Tiktoken`)**:
  - Optional BPE adapter for developers running OpenAI-only workloads seeking maximum Tiktoken encoding throughput.

---

## 3. Anti-Corruption Layer (ACL), Index Lifecycle & Security

> **Status:** Planned for Phase 2 (Story 2.2 — Document Ingestion)
1. **`M.E.DataIngestion` Anti-Corruption Layer (`IZVecTextChunker`)**:
   - Preview APIs in `Microsoft.Extensions.DataIngestion` are wrapped behind `IZVecTextChunker` and `IZVecDocumentReader` interfaces to prevent downstream breaking changes when Microsoft renames preview types.
> **Status:** Planned for Phase 2 (Story 1.11 — Embedder Stamp Manifest)
2. **Embedder Stamp Manifest (`zvec_index_manifest.json`)**:
   - On index creation, `ZVecIndexManifestManager` writes a manifest recording `ModelId`, `Dimensions`, and timestamp. Startup validation throws `ZVecEmbedderMismatchException` if configured embedders change, preventing index corruption.
> **Status:** Planned for Phase 2 (Story 2.3 — Optimize Lifecycle)
3. **`Optimize()` Lifecycle & Read-Write Lock**:
   - Batch ingestion automatically executes `collection.Optimize()`. Index handles are safely closed and reopened using a managed `ReaderWriterLockSlim` to ensure in-flight queries complete safely and post-optimize queries hit the merged HNSW graph.
> **Status:** Planned for Phase 2 (Story 2.6 — Threat Model & Security)
4. **Security Threat Model & Prompt Injection Sanitizer (`IRagSecuritySanitizer`)**:
   - Ingested/retrieved chunks pass through `IRagSecuritySanitizer` before prompt composition to escape system directive overrides. See [Security Threat Model](security-threat-model.md).
> **Status:** Planned for Phase 2 (Story 2.1 — IRagPipeline, Task 2.1.3)
5. **Context Window Token Budgeting (`MaxContextTokens`) & Multi-Turn History**:
   - Chunks are packed up to `MaxContextTokens` (default: 4096) using `Microsoft.ML.Tokenizers` before being submitted to `IChatClient`. Supports multi-turn chat history (`IList<ChatMessage>`).

---

## 4. Retrieval, Hybrid Search & Citation Generation

> **Status:** Planned for Phase 2 (Story 2.3 — Hybrid Search Bridge)
- **Hybrid Search**: Native ZVec dense vector search + FTS keyword matching fused via Reciprocal Rank Fusion (`ZVecRrfReranker`, default $k=60$).
> **Status:** Planned for Phase 2 (Story 2.3 — Citation Tracking)
- **Citation Tracking**: Round-trip metadata (`SourceDoc`, `SourceUri`, `SourceHash`, `Page`, `Offset`, `ChunkIndex`, `ChunkId`) into streaming `RagChunk` records, with distinct `RankScore` (RRF rank score for sorting), `DenseScore` (cosine similarity for thresholding), and `FtsScore` (BM25 keyword score).
> **Status:** Planned for Phase 2 (Story 2.3 — SSE Streaming)
- **SSE Response Helpers**: Real-time unbuffered Server-Sent Events endpoint helpers (`app.MapRagSseEndpoint(...)`) calling `Response.BodyWriter.FlushAsync()` after every chunk.


