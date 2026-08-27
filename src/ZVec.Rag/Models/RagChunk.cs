using Microsoft.Extensions.AI;

namespace ZVec.Rag.Models;

/// <summary>
/// A streaming RAG response chunk containing generated text and optional citations.
/// </summary>
/// <param name="Text">Generated token text for this chunk.</param>
/// <param name="Citations">Citations sorted by <see cref="CitationOrder"/> (Story 2.1: score descending only).</param>
/// <param name="IsFinal">Whether this is the terminal chunk in the stream.</param>
/// <param name="Usage">Optional token usage metadata on the final chunk.</param>
public sealed record RagChunk(
    string Text,
    IReadOnlyList<Citation> Citations,
    bool IsFinal,
    UsageDetails? Usage = null);
