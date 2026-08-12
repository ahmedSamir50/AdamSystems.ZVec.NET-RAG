# RAG Pipeline & Document Ingestion Architecture

`ZVec.Rag` provides a batteries-included RAG orchestration layer (`IRagPipeline`, `IRagIngestor`, `IRagRetriever`, `IRagGenerator`) built on top of Microsoft AI ecosystem primitives:

> **Status:** Planned for Phase 2 (Stories 2.1 – 2.6 — RAG Pipeline, Ingestion, Evaluation, SSE)
```text
┌─────────────────────────┐    ┌─────────────────────────┐    ┌─────────────────────────┐    ┌─────────────────────────┐
│   1. Document Reader    │ -> │    2. Text Chunker      │ -> │  3. Vector Embedder     │ -> │  4. Persistent Store    │
│  (PDF / HTML / MD / TXT │    │ (Token / Markdown AST / │    │ (IEmbeddingGenerator<   │    │(ZVec.VectorData +       │
│    / JSON Stream)       │    │  Sentence / Sliding)    │    │    string, Embedding>)  │    │     ZVec FTS Index)     │
└─────────────────────────┘    └─────────────────────────┘    └─────────────────────────┘    └─────────────────────────┘
```

---

## 1. Document Ingestion Pipeline Architecture (`IRagIngestor`)

Ingestion is transparently divided into four distinct, pluggable stages aligned with `Microsoft.Extensions.DataIngestion`:

1. **Document Readers (`IDocumentReader`)**:
   - `PlainTextDocumentReader` (Default): Fast UTF-8 stream reader for plain text and Markdown.
   - `PdfDocumentReader`: Pluggable reader for extracting structured text and layout metadata from PDF documents.
   - `HtmlDocumentReader`: DOM stripper for converting web pages into clean content streams.
2. **Text Chunkers (`ITextChunker`)**:
   - `TokenTextChunker` (Default): Splits text strictly on token boundaries using `Microsoft.ML.Tokenizers` (e.g. 512 tokens with 64-token overlap).
   - `MarkdownHeadingChunker`: AST-aware chunker preserving section titles (`# H1`, `## H2`) attached as metadata to child paragraphs.
   - `SentenceTextChunker`: Prevents splitting mid-sentence for high-precision semantic search.
3. **Deterministic Chunk ID Generator**:
   - Chunk IDs are generated using content-addressable SHA256 hashes: `ChunkId = SHA256(doc_uri | strategy_id | chunk_index)`. This ensures stability across re-ingestion and native content-based deduplication.
4. **Bounded Channel Dataflow Graph**:
   - Ingestion executes over bounded `System.Threading.Channels`: Document Parsing (Capacity 1024) $\rightarrow$ Deduplication (Capacity 2048) $\rightarrow$ Batch Embedding (Batch size 32) $\rightarrow$ Batch Vector Insertion (Batch size 100). Supports `IngestionCheckpoint` for interrupt-safe resume.

---

## 2. Tokenizer Architecture & RAG Evaluation Framework

- **Primary Tokenizer Engine (`Microsoft.ML.Tokenizers`)**: Zero-allocation Microsoft tokenizer engine supporting Tiktoken BPE (`cl100k_base`, `o200k_base`), SentencePiece, and WordPiece for 100% offline air-gapped execution.
- **RAG Evaluation Module (`IRagEvaluator`)**: Built-in evaluation framework in Phase 2 supporting:
  - `FaithfulnessEvaluator`: Validates whether generated answers strictly follow retrieved context using LLM-as-Judge (`IChatClient`).
  - `AnswerRelevanceEvaluator`: Measures how accurately the response addresses user intent.
  - `ContextPrecisionEvaluator`: Measures retrieval noise ratio and citation relevance.

---

## 3. Anti-Corruption Layer (ACL), Migration & Security

1. **`M.E.DataIngestion` Anti-Corruption Layer (`IRagChunker`)**: Wraps preview `M.E.DataIngestion` chunker APIs to isolate domain logic from upstream breaking changes.
2. **Embedder Stamp Manifest (`zvec_index_manifest.json`)**: On index creation, `ZVecIndexManifestManager` writes a manifest recording `ModelId`, `Dimensions`, and timestamp. Startup validation throws `ZVecEmbedderMismatchException` if configured embedders change.
3. **Embedding Migration Manager (`IRagMigrationManager`)**: Automates background re-indexing when embedding models or dimensions change, performing shadow collection builds and atomic index swaps.
4. **Security Threat Model & Prompt Isolation (`IRagSecuritySanitizer`)**: Ingested/retrieved chunks pass through `IRagSecuritySanitizer` before prompt composition. Uses query validation, chunk filtering, and explicit XML context isolation tags (`<retrieved_context>...</retrieved_context>`) to eliminate prompt injection risks.

---

## 4. Retrieval, Re-Ranking & Citation Generation

- **Hybrid Search & Fusion**: Native ZVec dense vector search + FTS keyword matching fused via Reciprocal Rank Fusion (`ZVecRrfReranker`, default $k=60$).
- **Re-Ranking Engines (`LlmReranker` / `ICrossEncoderReranker`)**: Pluggable re-ranking hook in Phase 2 enabling `LlmReranker` (via `IChatClient` prompt) and ONNX cross-encoders (`bge-reranker-v2-m3`).
- **Citation Tracking**: Round-trip metadata (`SourceDoc`, `SourceUri`, `SourceHash`, `Page`, `Offset`, `ChunkIndex`, `ChunkId`) into streaming `RagChunk` records, with distinct `RankScore`, `DenseScore`, and `FtsScore`.
- **SSE Response Helpers**: Real-time unbuffered Server-Sent Events endpoint helpers (`app.MapRagSseEndpoint(...)`) calling `Response.BodyWriter.FlushAsync()` after every chunk.



