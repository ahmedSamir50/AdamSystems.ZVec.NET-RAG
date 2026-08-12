# System Architecture Overview

`ZVec.NET-RAG` is structured around two main layers:

1. **`ZVec.Extensions.VectorData`**: The `Microsoft.Extensions.VectorData` connector that bridges ZVec.NET's native embedded vector DB engine with the .NET AI ecosystem.
2. **`ZVec.Rag`**: The application integration starter wiring document ingestion, embedding generators, retrieval, citation tracking, and streaming generation.

```
+-------------------------------------------------------------+
|                 Your .NET Application / API                 |
+------------------------------+------------------------------+
                               |
+------------------------------v------------------------------+
|                        ZVec.Rag                             |
|   • IRagPipeline orchestrator  • Citation tracking          |
|   • Streaming IAsyncEnumerable • SSE response helpers       |
+--------------+------------------------------+---------------+
               |                              |
+--------------v--------------+  +------------v---------------+
|  Microsoft.Extensions.AI    |  | ZVec.Extensions.VectorData|
|  • IChatClient              |  | • IVectorStore             |
|  • IEmbeddingGenerator      |  | • IVectorizedSearch<T>     |
+--------------+--------------+  +------------+---------------+
               |                              |
+--------------v--------------+  +------------v---------------+
| Local / Cloud Models        |  | ZVec.NET Engine            |
| (Ollama / Azure / ONNX)     |  | (In-process Native Vector) |
+-----------------------------+  +----------------------------+
```
