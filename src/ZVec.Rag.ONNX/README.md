# ZVec.Rag.ONNX

Optional ONNX Runtime embedder recipe for ZVec.Rag: `OnnxEmbedder`, `ClipImagePreprocessor`, and `ZVecRagMultimodalRecordV1`.

## Install

```bash
dotnet add package ZVec.Rag.ONNX
```

## Usage

```csharp
services.AddZVecRag(opts => { /* ... */ })
    .AddZVecRagOnnxEmbedder(o =>
    {
        o.ModelPath = Environment.GetEnvironmentVariable("ZVEC_ONNX_MODEL")!;
        o.ModelKind = OnnxEmbeddingModelKind.EmbeddingGemma;
        o.Dimensions = ZVecRagRecordV1.DefaultDimensions;
    });
```

ONNX model files are supplied by the application (not embedded in the NuGet).

## Platform scope

Desktop Windows, Linux, and macOS. Not trim-safe; not in pipeline Native AOT smoke.

## License

Apache-2.0
