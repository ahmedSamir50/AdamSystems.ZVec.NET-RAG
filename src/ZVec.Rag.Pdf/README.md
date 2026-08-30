# ZVec.Rag.Pdf

Optional PDF text extraction for ZVec.Rag ingestion using PdfPig 0.1.16.

## Install

```bash
dotnet add package ZVec.Rag.Pdf
```

## Usage

```csharp
services.AddZVecRag(opts => { /* ... */ })
    .AddTokenChunker()
    .AddZVecRagPdf();
```

PDF ingestion extracts page text only. Table-cell QA is post-v1.

## Native AOT

This package is not trim-safe. Do not reference it from Native AOT publish graphs.
