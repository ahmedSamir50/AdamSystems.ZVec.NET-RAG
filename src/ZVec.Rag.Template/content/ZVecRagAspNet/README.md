# ZVec RAG ASP.NET (TEMPLATE_LLM / TEMPLATE_EMBEDDER)

SSE chat at `/chat?question=` and ingest at `POST /ingest`.

Generated code uses `FakeChatClient` and `DeterministicEmbedder`.

<!--#if (llm == "fake")-->
Replace fakes with your `IChatClient` and `IEmbeddingGenerator` for production.
<!--#endif-->

```bash
dotnet run
```
