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

    // Hybrid Retrieval & Tuning (maps to ZVecHybridSearchOptions<ZVecRagRecordV1>)
    public int RrfK { get; set; } = 60;
    public int MaxContextTokens { get; set; } = 4096;
    public int GenerationReserveTokens { get; set; } = 512;
    public ContextPackingStrategy ContextPacking { get; set; } = ContextPackingStrategy.ScoreDescending;

    // Nested VectorStore options (shipped ZVec.Extensions.VectorData.Store.ZVecVectorStoreOptions)
    public ZVecVectorStoreOptions VectorStore { get; set; } = new();

    // Application logging verbosity for RAG pipeline components (ILogger), not a native ZVec engine field
    public LogLevel LogLevel { get; set; } = LogLevel.Warning;
}
```

`ZVecVectorStoreOptions` (connector) exposes: `StoragePath`, `MaxConcurrentNativeCalls`, `EnableMmap`, `ReadOnly`, `MemoryLimitMb`, `ModelId`, `DefaultQuantizeType`. `AddZVecRag` copies `ZVecRagOptions.StoragePath` and `ModelId` into `VectorStore` when unset.

`AddZVecVectorStore` registers `IZvecFactory`, `ZVecVectorStore` (`ZVec.Extensions.VectorData.Store`), and `VectorStore` as singletons. It also registers `ZVecFactoryShutdownRegistration` (`IHostedService`) to call `IZvecFactory.Shutdown()` on `IHostApplicationLifetime.ApplicationStopping`. `ZVecVectorStoreOptions` maps to engine options: `MaxConcurrentNativeCalls` and `MemoryLimitMb` → `ZVecOptions`; `EnableMmap` and `ReadOnly` → `ZVecCollectionOptions` on `OpenOrCreate`; `DefaultQuantizeType` → HNSW index params via `ZVecVectorIndexResolver`; `ModelId` → embedder stamp sidecar via `ZVecIndexManifestManager`.

---

## 🔒 Process Teardown & iOS MonoAOT Finalizer Safety

Native C++ collection handles must be closed cleanly upon application shutdown to flush write-ahead logs (WAL) and prevent finalizer thread deadlocks during mobile process suspension.

`AddZVecVectorStore` registers `ZVecFactoryShutdownRegistration`, which hooks `IHostApplicationLifetime.ApplicationStopping` and calls `IZvecFactory.Shutdown()`:

```csharp
services.AddZVecVectorStore(); // registers shutdown on ApplicationStopping when hosted
```

Future `AddZVecRag` idempotently calls `AddZVecVectorStore`, so connector apps get the same teardown without duplicating the hook.

`AddZVecRag` is shipped in Story 2.1 and registers scoped `IRagIngestor`, `IRagRetriever`, `IRagGenerator`, `IRagPipeline`, and a per-scope `RagCollectionProvider` (single native collection handle per scope).
