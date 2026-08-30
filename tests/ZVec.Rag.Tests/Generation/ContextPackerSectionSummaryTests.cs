using ZVec.Rag.Constants;
using ZVec.Rag.Generation;
using ZVec.Rag.Models;

namespace ZVec.Rag.Tests.Generation;

public sealed class ContextPackerSectionSummaryTests
{
    private static Citation CreateCitation(
        string chunkId,
        int chunkIndex,
        float rankScore,
        string text,
        string sectionSummary = "")
    {
        return new Citation(
            "doc-1",
            "uri-1",
            "hash-1",
            null,
            0,
            chunkIndex,
            chunkId,
            text,
            rankScore,
            rankScore,
            0f,
            "summary-1",
            sectionSummary);
    }

    [Fact]
    public void Pack_PrependsSummaryBeforeRetrievedContext_AndKeepsChildTextInCitation()
    {
        var packer = new ContextPacker();
        const string summaryText = "Section overview for packing.";
        var citations = new[]
        {
            CreateCitation("chunk-0", 0, 0.9f, "child chunk body", summaryText)
        };

        ContextPackResult result = packer.Pack(
            citations,
            maxContextTokens: 4096,
            generationReserveTokens: 0,
            historyTokenEstimate: 0,
            ContextPackingStrategy.ScoreDescending);

        int summaryIndex = result.ContextBlock.IndexOf(ZVecRagConstants.SectionSummaryOpenTag, StringComparison.Ordinal);
        int contextIndex = result.ContextBlock.IndexOf(ZVecRagConstants.RetrievedContextOpenTag, StringComparison.Ordinal);

        Assert.True(summaryIndex >= 0);
        Assert.True(contextIndex > summaryIndex);
        Assert.Contains(summaryText, result.ContextBlock, StringComparison.Ordinal);
        Assert.Equal("child chunk body", result.PackedCitations[0].Text);
    }

    [Fact]
    public void Pack_LitmStrategy_DoesNotAlterCitationIdentityFields()
    {
        var packer = new ContextPacker();
        var citations = Enumerable.Range(0, 5)
            .Select(i => CreateCitation($"chunk-{i}", i, 1f - (i * 0.1f), $"text {i}", $"summary {i % 2}"))
            .ToList();

        ContextPackResult result = packer.Pack(
            citations,
            maxContextTokens: 8192,
            generationReserveTokens: 0,
            historyTokenEstimate: 0,
            ContextPackingStrategy.LostInTheMiddle);

        foreach (Citation packed in result.PackedCitations)
        {
            Citation original = citations.Single(c => c.ChunkId == packed.ChunkId);
            Assert.Equal(original.ChunkIndex, packed.ChunkIndex);
            Assert.Equal(original.RankScore, packed.RankScore);
        }
    }
}
