using System.Threading.Channels;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;
using ZVec.Rag.Models;

namespace ZVec.Rag.Ingestion;

/// <summary>
/// Pushes synchronous chunker output into a bounded channel (no background Task.Run wrapper).
/// </summary>
public static class IngestionChannelPump
{
    /// <summary>
    /// Enumerates chunker output into a bounded channel, applying backpressure when full.
    /// </summary>
    public static async Task<int> PumpChunksAsync(
        IZVecTextChunker chunker,
        string text,
        ChannelWriter<TextChunk> writer,
        CancellationToken cancellationToken)
    {
        int count = 0;
        foreach (TextChunk chunk in chunker.Chunk(text))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
            count++;
        }

        return count;
    }

    /// <summary>
    /// Creates a bounded parse channel with the standard ingest capacity.
    /// </summary>
    public static Channel<TextChunk> CreateParseChannel()
        => Channel.CreateBounded<TextChunk>(new BoundedChannelOptions(ZVecRagConstants.ParseChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });
}
