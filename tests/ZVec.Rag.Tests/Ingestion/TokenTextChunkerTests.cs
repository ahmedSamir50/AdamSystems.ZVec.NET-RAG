using Microsoft.ML.Tokenizers;
using ZVec.Rag.Constants;
using ZVec.Rag.Ingestion;

namespace ZVec.Rag.Tests.Ingestion;

/// <summary>
/// Unit tests for <see cref="TokenTextChunker"/>.
/// </summary>
public sealed class TokenTextChunkerTests
{
  private static TokenTextChunker CreateChunker(int maxTokens = 8, int overlap = 2)
        => new TokenTextChunker(TiktokenTokenizer.CreateForEncoding(ZVecRagConstants.Cl100kBaseEncoding), maxTokens, overlap);

    [Fact]
    public void Chunk_ShortText_ReturnsSingleChunk()
    {
        var chunker = CreateChunker();
        var chunks = chunker.Chunk("Hello world").ToList();

        Assert.Single(chunks);
        Assert.Equal(0, chunks[0].Offset);
    }

    [Fact]
    public void Chunk_LongText_ReturnsMultipleChunks()
    {
        string text = string.Join(' ', Enumerable.Range(0, 200).Select(i => $"token{i}"));
        var chunker = CreateChunker(maxTokens: 16, overlap: 4);
        var chunks = chunker.Chunk(text).ToList();

        Assert.True(chunks.Count > 1);
        Assert.Equal(ZVecRagConstants.TokenChunkerStrategyId, chunker.StrategyId);
    }
}
