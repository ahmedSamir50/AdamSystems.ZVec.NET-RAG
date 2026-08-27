# System Architecture Overview

`ZVec.NET-RAG` is structured around two main layers:

1. **`ZVec.Extensions.VectorData`**: The `Microsoft.Extensions.VectorData` connector that bridges ZVec.NET's native embedded vector DB engine with the .NET AI ecosystem.
2. **`ZVec.Rag`**: The application integration starter wiring document ingestion, embedding generators, retrieval, citation tracking, and streaming generation.

```mermaid
flowchart TB
  subgraph appLayer ["Your .NET Application / API"]
    appNode[Application]
  end
  subgraph ragLayer ["ZVec.Rag"]
    ragNode["IRagPipeline orchestrator\nCitation tracking\nStreaming IAsyncEnumerable\nSSE response helpers"]
  end
  subgraph meaiLayer ["Microsoft.Extensions.AI"]
    meaiNode["IChatClient\nIEmbeddingGenerator"]
  end
  subgraph vdLayer ["ZVec.Extensions.VectorData"]
    vdNode["IVectorStore\nIVectorizedSearch T"]
  end
  subgraph modelsLayer ["Local / Cloud Models"]
    modelsNode["Ollama / Azure / ONNX"]
  end
  subgraph engineLayer ["ZVec.NET Engine"]
    engineNode["In-process Native Vector"]
  end
  appNode --> ragNode
  ragNode --> meaiNode
  ragNode --> vdNode
  meaiNode --> modelsNode
  vdNode --> engineNode
```
