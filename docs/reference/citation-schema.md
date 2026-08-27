# Citation Schema & String Immutability Contract

This reference document defines the canonical chunk metadata schema (`ZVecRagSchemaV1`) used by `ZVec.Rag` and documents the create-time string field immutability constraint imposed by ZVec.NET's native DDL engine.

---

## ⚠️ ZVec.NET Native DDL Immutability Constraint

> [!CAUTION]
> Native ZVec `add_column` DDL operations and typed `EnsureSchema` methods **only support adding nullable numeric columns** (`int`, `long`, `float`, `double`) to existing collections. **String and array fields cannot be added via DDL migration post-creation.**

To prevent destructive collection recreation and re-ingestion, `ZVec.Rag` standardizes on a forward-compatible create-time schema containing all standard metadata fields at initial collection initialization.

---

## 📜 Canonical Chunk Schema (`ZVecRagSchemaV1`)

Every collection initialized by `ZVec.Rag` automatically uses `ZVecRagSchemaV1`:

```csharp
public sealed class ZVecRagRecordV1
{
    [VectorStoreKey]
    [ZVecId]
    public string ChunkId { get; set; } = string.Empty; // SHA256(source_uri | strategy_id | chunk_index)

    [VectorStoreData(IsIndexed = true)]
    [ZVecField]
    public string SourceDoc { get; set; } = string.Empty; // Document GUID / stable identifier

    [VectorStoreData]
    [ZVecField]
    public string SourceUri { get; set; } = string.Empty; // Display URI / file path / URL

    [VectorStoreData(IsIndexed = true)]
    [ZVecField]
    public string SourceHash { get; set; } = string.Empty; // SHA-256 content hash for deduplication

    [VectorStoreData(IsIndexed = true)]
    [ZVecField]
    public int Page { get; set; } = -1; // -1 = not applicable (maps to null in Citation)

    [VectorStoreData]
    [ZVecField]
    public long Offset { get; set; } // Character offset in extracted text

    [VectorStoreData]
    [ZVecField]
    public int ChunkIndex { get; set; } // 0-indexed chunk sequence number in document

    [VectorStoreData(IsFullTextIndexed = true)]
    [ZVecField]
    public string Text { get; set; } = string.Empty; // Chunk text content (FTS field)

    [VectorStoreVector(768)]
    [ZVecVector(768)]
    public ReadOnlyMemory<float> DenseVector { get; set; } // Dense vector embedding
}
```

### ChunkId Generation (D-4)

`ChunkId` is a **content-addressable** SHA-256 hex digest:

`ChunkId = SHA256(source_uri | strategy_id | chunk_index)`

- `source_uri`: document URI or stable `documentId` passed to ingest
- `strategy_id`: chunking strategy identifier (Story 2.1 default ingest: `"token-v1"` via `TokenTextChunker`; whole-text path uses `"whole-text-v1"`)
- `chunk_index`: 0-based chunk sequence within the document

Human-readable display labels (e.g. `{SourceDoc}:{ChunkIndex:D6}`) are **not** used as storage keys.

---

## 📊 Citation Record Structure & Score Semantics

When `IRagRetriever` or `IRagGenerator` returns citations, the `Citation` object distinguishes between RRF rank-based scores and cosine similarity scores:

```csharp
public sealed record Citation(
    string SourceDoc,
    string SourceUri,
    string SourceHash,
    int? Page,
    long Offset,
    int ChunkIndex,
    string ChunkId,
    float RankScore,  // Fused RRF Rank Score: 1/(k + rank) -> Used strictly for sorting
    float DenseScore, // Cosine Similarity: 1.0 - ZVecDistance -> Used for threshold filtering (>0.7)
    float FtsScore    // BM25 Text Relevance Score -> Unbounded positive float
);
```

### Score Semantics Table

| Field | Range | Meaning | Primary Use Case |
|---|---|---|---|
| `RankScore` | `0.0` – `0.0164` | Reciprocal Rank Fusion score ($1/(k + \text{rank})$ for $k=60$). | Sorting results in hybrid search mode. |
| `DenseScore` | `0.0` – `1.0` | Normalized Cosine similarity ($1.0 - \text{distance}$). | **Threshold filtering** (e.g. `citation.DenseScore > 0.75`). |
| `FtsScore` | `0.0` – $\infty$ | Raw BM25 keyword relevance score. | Diagnostic inspection of keyword matches. |
| `Offset` | $0$ – $\text{Length}$ | 0-based **character offset** in extracted text. | UI text highlighting in document preview. |
| `Page` | $1$ – $N$ or `null` | 1-based page number in PDF/DOCX (`null` for plain text/MD). | Page-specific citation rendering ("Page 42"). |

---

## Prompt Order vs Citation List Order

`ContextPacker` (Story 2.1.3) and `CitationOrder` (Story 2.3.2) are **independent**:

| Concern | Controlled by | Applies to |
|---|---|---|
| LLM context block order | `ContextPackingStrategy` (`ScoreDescending` default, optional `LostInTheMiddle`) | `<retrieved_context>` text in the prompt |
| UI / API citation list order | `CitationOrder` (`ScoreDescending`, `ChunkOrderAscending`, etc.) | `RagChunk.Citations` collection |

When `LostInTheMiddle` permutes prompt slots (e.g. `[C1, C5, C3, C4, C2]`), each `Citation` record **retains** its original `ChunkId`, `ChunkIndex`, and `RankScore`. The UI sorts by `CitationOrder` — not by prompt position. If the LLM emits citation markers, they must reference stable `ChunkId` labels, not 1-based indices into the permuted prompt.

