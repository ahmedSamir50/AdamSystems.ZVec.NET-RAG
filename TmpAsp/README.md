# ZVec RAG ASP.NET (fake / fake)

SSE chat at `/chat?question=` and ingest at `POST /ingest`.

Generated code uses `FakeChatClient` and `DeterministicEmbedder`.

Replace fakes with your `IChatClient` and `IEmbeddingGenerator` for production.

```bash
dotnet run
```
