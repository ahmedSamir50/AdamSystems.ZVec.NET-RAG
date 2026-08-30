# ZVec.Rag.Template

Install:

```bash
dotnet new install ZVec.Rag.Template
```

Templates:

| shortName | Description |
|---|---|
| `zvec-rag` | Console RAG app with deterministic fakes |
| `zvec-rag-aspnet` | ASP.NET Core SSE chat + ingest endpoint |
| `zvec-rag-maui` | MAUI Blazor Hybrid retrieve-only placeholder |

Generated code always uses `FakeChatClient` (a test double that does not call a model) and `DeterministicEmbedder`. Replace them with your `IChatClient` and `IEmbeddingGenerator` before production. Symbol choices update README hints only.
