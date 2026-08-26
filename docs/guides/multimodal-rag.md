# Multimodal RAG (Text + Image) Pipeline Guide

This guide describes how to implement multimodal Retrieval-Augmented Generation (text queries retrieving image chunks or joint image-text embeddings) using `ZVec.Rag.ONNX` and CLIP models.

---

## 🖼️ Multimodal Architecture

CLIP (Contrastive Language-Image Pre-Training) maps both text prompts and images into a shared multi-dimensional vector space.

```
┌─────────────────────────┐
│       Text Query        │ -> CLIP Text Encoder  ─┐
└─────────────────────────┘                        │
                                                   ├─> Shared Vector Space (ZVec Vector Store)
┌─────────────────────────┐                        │
│       Image File        │ -> CLIP Vision Encoder ┘
└─────────────────────────┘
```

---

## ⚡ Image Preprocessing Pipeline (`SixLabors.ImageSharp`)

Image preprocessing before passing raw pixels into the CLIP vision ONNX model requires image loading, resizing, cropping, and tensor normalization.

To ensure **100% Native AOT compatibility** and cross-platform execution, `ZVec.Rag.ONNX` uses `SixLabors.ImageSharp` for image preprocessing:

```csharp
public sealed class ClipImagePreprocessor
{
    private static readonly float[] Mean = [0.48145466f, 0.4578275f, 0.40821073f];
    private static readonly float[] Std = [0.26862954f, 0.26130258f, 0.27577711f];

    public Tensor<float> Preprocess(Stream imageStream, int targetSize = 224)
    {
        using var image = Image.Load<Rgb24>(imageStream);
        
        // 1. Resize image preserving aspect ratio & crop center
        image.Mutate(x => x.Resize(new ResizeOptions {
            Size = new Size(targetSize, targetSize),
            Mode = ResizeMode.Crop
        }));

        // 2. Normalize RGB values to NCHW Tensor format [1, 3, 224, 224]
        var tensor = new DenseTensor<float>(new[] { 1, 3, targetSize, targetSize });
        
        image.ProcessPixelRows(accessor => {
            for (int y = 0; y < accessor.Height; y++)
            {
                var pixelRow = accessor.GetRowSpan(y);
                for (int x = 0; x < accessor.Width; x++)
                {
                    tensor[0, 0, y, x] = (pixelRow[x].R / 255.0f - Mean[0]) / Std[0];
                    tensor[0, 1, y, x] = (pixelRow[x].G / 255.0f - Mean[1]) / Std[1];
                    tensor[0, 2, y, x] = (pixelRow[x].B / 255.0f - Mean[2]) / Std[2];
                }
            }
        });

        return tensor;
    }
}
```

---

## 🖥️ Platform Scope & Model Distribution

> [!IMPORTANT]
> - **Desktop Only (Windows / Linux / macOS)**: Multimodal CLIP models (~600 MB ONNX file size) and ONNX Runtime execution providers are intended for Desktop and Server scenarios. They are **not recommended for mobile app distribution**.
> - **One embedder per collection**: CLIP text and image vectors share one space by design. Do **not** mix CLIP with MiniLM in one collection — use Story 1.11 embedder stamp (`ModelId` + dimensions) to enforce consistency.
> - **No `[ZVecModality]` source generator**: Use an ordinary indexed POCO field `SourceKind` (`text` | `image`) for UI/citations. Filter with LINQ (`r => r.SourceKind == "image"`) only when the product wants unimodal results — not inside `SearchAsync` by default.
> - **Model File Provisioning**: ONNX model files are not embedded inside NuGet packages to keep download sizes small. Application developers must supply model file paths or download models dynamically on first launch.
