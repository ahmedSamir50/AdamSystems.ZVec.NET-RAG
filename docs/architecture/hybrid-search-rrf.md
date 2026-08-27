# Hybrid Search & RRF Math

> **Status:** Implemented in `ZVec.Extensions.VectorData` (Story 1.7). `ZVecVectorizableRecordCollection<TRecord, TKey>` implements `IKeywordHybridSearchable<TRecord>` and delegates native dense + FTS fusion to `ZVec.NET` with optional `ZVecRrfReranker` rank constant (`ZVecHybridSearchOptions<TRecord>.RrfK`).

ZVec.NET supports native in-database dense vector and Full-Text Search (FTS) hybrid queries re-ranked via **Reciprocal Rank Fusion (RRF)**.

## RRF Formula

The Reciprocal Rank Fusion score $RRF(d)$ for document $d$ across multiple result lists $R$ is defined as:

$$RRF(d) = \sum_{r \in R} \frac{1}{k + r(d)}$$

Where:
- $r(d)$ is the rank of document $d$ in result list $r$ (1-indexed).
- $k$ is a smoothing constant (default $k = 60$).

In `ZVec.NET`, `ZVecRrfReranker` computes this natively inside the C++ core engine during hybrid query execution.

## Connector Usage (`IKeywordHybridSearchable`)

```csharp
IKeywordHybridSearchable<MyRecord> hybrid = collection;
var options = new ZVecHybridSearchOptions<MyRecord>
{
    RrfK = 60,
    Filter = r => r.Category == "books",          // scalar indexed fields only (not FTS columns)
    AdditionalProperty = r => r.Content           // optional FTS field override
};

await foreach (var hit in hybrid.HybridSearchAsync(
    queryVector, keywords: new[] { "vector database" }, top: 10, options, ct))
{
    // Score is raw RRF rank fusion (1/(k+rank)); not cosine-normalized dense distance
    Console.WriteLine($"{hit.Record.Id} score={hit.Score}");
}
```

Register the vector store with native concurrency throttling:

```csharp
services.AddZVecVectorStore(opts =>
{
    opts.StoragePath = "./data";
    opts.MaxConcurrentNativeCalls = Environment.ProcessorCount; // default
});
```
