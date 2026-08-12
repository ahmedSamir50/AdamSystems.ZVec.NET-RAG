# ZVec.Rag — Local-First RAG for .NET

Welcome to the official technical wiki for **`ZVec.Rag`** and **`ZVec.Extensions.VectorData`**.

> **"Local-first RAG for .NET. No cloud. No Python. No kidding."**

---

## 🏛️ Project Purpose

`ZVec.Rag` brings high-performance, embedded, local-first RAG (Retrieval-Augmented Generation) capabilities to the .NET ecosystem. Built directly on top of the native vector database engine [`ZVec.NET`](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET) and Microsoft's official AI abstractions (`Microsoft.Extensions.VectorData` and `Microsoft.Extensions.AI`), it provides:

1. **`ZVec.Extensions.VectorData`**: A first-party style connector enabling any .NET app using `IVectorStore` or `IVectorizedSearch<TRecord>` to persist vectors locally with zero cloud dependencies.
2. **`ZVec.Rag`**: A batteries-included integration library wiring document ingestion, hybrid search, citation tracking, and streaming generation.
3. **`ZVec.Rag.Template`**: A `dotnet new rag` template that scaffolds a working RAG solution in 60 seconds.

---

## 📚 Technical Wiki Structure

- **[Architecture & Theory](architecture/overview.md)**: System design, vector math, hybrid search Reciprocal Rank Fusion (RRF) algorithms, and Native AOT zero-copy memory management.
- **[Testing Strategy](guides/testing-strategy.md)**: Strict Test-Driven Development (TDD) workflow, branch coverage requirements, and mock-free CI testing.
- **[Code Standards](guides/code-standards.md)**: Enums vs magic strings, XML documentation standards, Single Responsibility Principle (no God classes/methods), and Code Reviewer agent gates.
- **[API Reference](reference/api.md)**: Comprehensive public API surface documentation.
