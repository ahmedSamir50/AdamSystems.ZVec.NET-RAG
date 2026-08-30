using Microsoft.Extensions.AI;

namespace ZVec.Rag.LLamaSharp;

/// <summary>
/// Test seam and production abstraction over LLamaSharp native inference.
/// </summary>
internal interface ILlamaSharpSession : IDisposable
{
    /// <summary>Streams generated tokens for the given chat messages.</summary>
    IAsyncEnumerable<string> GenerateStreamingAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken);

    /// <summary>Embeds a single text string into a dense vector.</summary>
    ValueTask<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken cancellationToken);
}
