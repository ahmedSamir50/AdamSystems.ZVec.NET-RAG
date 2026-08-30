# ZVec.Rag.LLamaSharp

Optional LLamaSharp adapters for ZVec.Rag: `LLamaSharpChatClient` and `LLamaSharpEmbedder`.

## Install

```bash
dotnet add package ZVec.Rag.LLamaSharp
```

## Usage

```csharp
services.AddZVecRag(opts => { /* ... */ })
    .AddZVecRagLLamaSharp(o => o.ModelPath = Environment.GetEnvironmentVariable("ZVEC_LLAMA_MODEL")!);
```

Or construct adapters directly:

```csharp
opts.Chat = new LLamaSharpChatClient(new LLamaSharpOptions { ModelPath = modelPath });
```

## Platform scope

Desktop Windows, Linux, and macOS only. Not trim-safe; not in pipeline Native AOT smoke. Not for MAUI.

## License

Apache-2.0
