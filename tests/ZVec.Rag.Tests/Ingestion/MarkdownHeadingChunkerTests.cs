using Microsoft.ML.Tokenizers;
using ZVec.Rag.Constants;
using ZVec.Rag.Ingestion;

namespace ZVec.Rag.Tests.Ingestion;

/// <summary>
/// Unit tests for <see cref="MarkdownHeadingChunker"/>.
/// </summary>
public sealed class MarkdownHeadingChunkerTests
{
    [Fact]
    public void Chunk_SplitsOnHeadings()
    {
        var tokenizer = TiktokenTokenizer.CreateForEncoding(ZVecRagConstants.Cl100kBaseEncoding);
        var chunker = new MarkdownHeadingChunker(new TokenTextChunker(tokenizer, 512, 64));
        string md = "# Title\n\nBody one.\n\n## Section\n\nBody two.";

        var chunks = chunker.Chunk(md).ToList();

        Assert.True(chunks.Count >= 2);
        Assert.Equal(ZVecRagConstants.MarkdownHeadingChunkerStrategyId, chunker.StrategyId);
    }
}
