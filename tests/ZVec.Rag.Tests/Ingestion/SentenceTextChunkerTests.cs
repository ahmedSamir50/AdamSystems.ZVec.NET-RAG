using ZVec.Rag.Ingestion;

namespace ZVec.Rag.Tests.Ingestion;

/// <summary>
/// Unit tests for <see cref="SentenceTextChunker"/>.
/// </summary>
public sealed class SentenceTextChunkerTests
{
    [Fact]
    public void Chunk_DoesNotSplitMidSentence()
    {
        var chunker = new SentenceTextChunker();
        var chunks = chunker.Chunk("First sentence. Second sentence! Third?").ToList();

        Assert.Equal(3, chunks.Count);
        Assert.All(chunks, c => Assert.True(c.Text.EndsWith('.') || c.Text.EndsWith('!') || c.Text.EndsWith('?')));
    }
}
