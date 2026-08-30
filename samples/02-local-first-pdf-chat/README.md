# Sample 02 — Local-first PDF chat

ASP.NET SSE chat with English, Arabic, and PDF ingestion via `ZVec.Rag.Pdf`.

This sample extracts PDF text only. Table-cell QA is post-v1 (Epic 8.7 / D-7).

```bash
dotnet run --project samples/02-local-first-pdf-chat/ZVec.Rag.Sample02.csproj
```

Chat: `GET /chat?question=What is ZVec?`
