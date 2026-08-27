using Microsoft.Extensions.AI;
using ZVec.Rag.Models;

namespace ZVec.Rag.Abstractions;

/// <summary>
/// LLM generation with RAG context packing and streaming.
/// </summary>
public interface IRagGenerator
{
    /// <summary>Retrieves context, packs a token-budgeted prompt, and streams the LLM answer.</summary>
    IAsyncEnumerable<RagChunk> AskAsync(
        string question,
        IList<ChatMessage>? history = null,
        bool streamCitations = true,
        CancellationToken cancellationToken = default);
}
