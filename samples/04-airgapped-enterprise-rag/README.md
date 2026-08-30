# Sample 04 — Air-gapped enterprise RAG

ASP.NET SSE endpoint with `FakeChatClient` for CI. Set `ZVEC_LLAMA_MODEL` for optional local LLamaSharp chat via `ZVec.Rag.LLamaSharp`. Set `ZVEC_LLAMA_EMBED=1` to also use `LLamaSharpEmbedder` (otherwise `DeterministicEmbedder`).

```bash
dotnet run --project samples/04-airgapped-enterprise-rag/ZVec.Rag.Sample04.csproj
```
