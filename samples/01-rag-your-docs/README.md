# Sample 01 — RAG your docs

Minimal local-first RAG using `IRagPipeline`, `DeterministicEmbedder`, and `FakeChatClient`.

```bash
dotnet run --project samples/01-rag-your-docs/ZVec.Rag.Sample01.csproj
```

Ingests `docs/hello.md`, asks "What is ZVec?", streams the fake LLM reply, and prints a citation chunk id and source document.
