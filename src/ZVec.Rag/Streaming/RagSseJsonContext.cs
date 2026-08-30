using System.Text.Json.Serialization;
using ZVec.Rag.Models;

namespace ZVec.Rag.Streaming;

/// <summary>
/// Wire payload for SSE streaming of <see cref="RagChunk"/> events.
/// </summary>
internal sealed record RagSsePayload(string Text, bool IsFinal, IReadOnlyList<Citation> Citations);

/// <summary>
/// AOT-safe JSON serialization for SSE payloads.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RagSsePayload))]
[JsonSerializable(typeof(Citation))]
internal partial class RagSseJsonContext : JsonSerializerContext;
