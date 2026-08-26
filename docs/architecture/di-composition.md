# Dependency Injection & Lifecycle Composition

This document details the Dependency Injection (DI) composition hierarchy, registration options, and lifetime contracts across `ZVec.NET`, `ZVec.Extensions.VectorData`, and `ZVec.Rag`.

---

## 🏗️ Composition Hierarchy

`AddZVecRag` serves as the top-level composition entry point. It configures and registers `ZVec.Extensions.VectorData` (`AddZVecVectorStore`) and `ZVec.NET` engine services (`AddZVec`) idempotently if they have not already been pre-registered.

```
AddZVecRag(opts => { ... })
  ├── Idempotently calls AddZVecVectorStore(opts.VectorStore)
  │     └── Idempotently calls AddZVec(opts.ZVec)
  │           └── Registers IZvecFactory (Singleton)
  │     └── Registers IVectorStore (Singleton)
  │     └── Registers IVectorStoreRecordCollection<TKey, TRecord> (Singleton)
  ├── Registers IRagIngestor (Scoped)
  ├── Registers IRagRetriever (Scoped)
  ├── Registers IRagGenerator (Scoped)
  └── Registers IRagPipeline (Scoped, Composite Facade)
```

---

## ⏱️ Service Lifetimes Matrix

| Service Interface | Concrete Implementation | DI Lifetime | Rationale & Lifecycle Contract |
|---|---|---|---|
| `IZvecFactory` | `ZVecFactory` | **Singleton** | Holds native C++ library handles (`SafeZvecHandle`). Must survive process lifetime. Shut down via `ApplicationStopping`. |
| `IVectorStore` | `ZVecVectorStore` | **Singleton** | Thread-safe entry point for collection management and listing. |
| `IVectorStoreRecordCollection<TKey, TRecord>` | `ZVecVectorizableRecordCollection` | **Singleton** | Holds collection file handle. Concurrent reads + optimize/reopen via shipped `OptimizeAndReopenAsync` (`lock (_initLock)`; native `MaxConcurrentReads` throttle). |
| `IRagIngestor` | `RagIngestor` | **Scoped** | Per-request/operation document ingestion state and batch channel buffers. |
| `IRagRetriever` | `RagRetriever` | **Scoped** | Per-request query tokenization and candidate ranking. |
| `IRagGenerator` | `RagGenerator` | **Scoped** | Per-request LLM streaming state, context window budget manager, and HTTP client references. |
| `IRagPipeline` | `RagPipeline` | **Scoped** | Composite facade delegating to scoped sub-services. |

---

## ⚙️ Configuration Options Hierarchy

```csharp
public sealed class ZVecRagOptions
{
    public string StoragePath { get; set; } = "./rag.zvec";
    
    // Microsoft.Extensions.AI Component Binding
    public IEmbeddingGenerator<string, Embedding<float>>? Embedder { get; set; }
    public IChatClient? Chat { get; set; }
    
    // Hybrid Retrieval & Tuning
    public HybridSearchOptions HybridSearch { get; set; } = new();
    
    // Nested VectorStore & Engine Throttles
    public ZVecVectorStoreOptions VectorStore { get; set; } = new();
    public ZVecEngineOptions ZVec { get; set; } = new();
}

public sealed class ZVecVectorStoreOptions
{
    public string StoragePath { get; set; } = string.Empty;
    public int MaxConcurrentNativeCalls { get; set; } = Environment.ProcessorCount;
    public bool EnableMmap { get; set; } = true;
    public bool ReadOnly { get; set; }
    public int? MemoryLimitMb { get; set; }
    public ZVecQuantizeType DefaultQuantizeType { get; set; } = ZVecQuantizeType.Undefined;
}

public sealed class ZVecEngineOptions
{
    public int MaxConcurrentNativeCalls { get; set; } = Environment.ProcessorCount;
    public LogLevel LogLevel { get; set; } = LogLevel.Warning;
}
```

`AddZVecVectorStore` registers `IZvecFactory`, `ZVecVectorStore` (`ZVec.Extensions.VectorData.Store`), and `VectorStore` as singletons. `ZVecVectorStoreOptions` maps to engine options: `MaxConcurrentNativeCalls` and `MemoryLimitMb` → `ZVecOptions`; `EnableMmap` and `ReadOnly` → `ZVecCollectionOptions` on `OpenOrCreate`; `DefaultQuantizeType` → HNSW index params via `ZVecVectorIndexResolver`.

---

## 🔒 Process Teardown & iOS MonoAOT Finalizer Safety

Native C++ collection handles must be closed cleanly upon application shutdown to flush write-ahead logs (WAL) and prevent finalizer thread deadlocks during mobile process suspension.

`AddZVecRag` automatically hooks into `IHostApplicationLifetime`:

```csharp
appLifetime.ApplicationStopping.Register(() => {
    factory.Shutdown(); // Gracefully closes all SafeZvecHandle instances
});
```
