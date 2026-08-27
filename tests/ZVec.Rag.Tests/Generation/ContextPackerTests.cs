using ZVec.Rag.Generation;
using ZVec.Rag.Models;

namespace ZVec.Rag.Tests.Generation;

/// <summary>
/// Unit tests for <see cref="ContextPacker"/>.
/// </summary>
public sealed class ContextPackerTests
{
    private static Citation CreateCitation(int index, float score, string text)
    {
        return new Citation(
            $"doc-{index}",
            $"uri-{index}",
            $"hash-{index}",
            null,
            index * 10,
            index,
            $"chunk-{index}",
            text,
            score,
            score,
            0f);
    }

    [Fact]
    public void ApplyLostInTheMiddle_PermutesOrder_WithoutChangingIdentityFields()
    {
        var citations = Enumerable.Range(0, 5)
            .Select(i => CreateCitation(i, 1f - (i * 0.1f), $"text-{i}"))
            .ToList();

        IReadOnlyList<Citation> permuted = ContextPacker.ApplyLostInTheMiddle(citations);

        Assert.Equal(5, permuted.Count);
        Assert.Equal(citations.Select(c => c.ChunkId).OrderBy(id => id), permuted.Select(c => c.ChunkId).OrderBy(id => id));
        Assert.NotEqual(citations.Select(c => c.ChunkId).ToArray(), permuted.Select(c => c.ChunkId).ToArray());
        foreach (var original in citations)
        {
            Citation packed = permuted.Single(c => c.ChunkId == original.ChunkId);
            Assert.Equal(original.ChunkIndex, packed.ChunkIndex);
            Assert.Equal(original.RankScore, packed.RankScore);
        }
    }

    [Fact]
    public void Pack_RespectsTokenBudget_AndOmitsLowerRankChunks()
    {
        var packer = new ContextPacker();
        var citations = new[]
        {
            CreateCitation(0, 0.9f, new string('a', 200)),
            CreateCitation(1, 0.8f, new string('b', 200)),
            CreateCitation(2, 0.1f, new string('c', 200))
        };

        ContextPackResult result = packer.Pack(
            citations,
            maxContextTokens: 80,
            generationReserveTokens: 10,
            historyTokenEstimate: 10,
            ContextPackingStrategy.ScoreDescending);

        Assert.True(result.PackedCitations.Count < citations.Length);
        Assert.Contains(result.PackedCitations, c => c.ChunkId == "chunk-0");
        Assert.DoesNotContain(result.PackedCitations, c => c.ChunkId == "chunk-2");
    }

    [Fact]
    public void Pack_LitmStrategy_DoesNotAlterCitationIdentity_InPackedList()
    {
        var packer = new ContextPacker();
        var citations = Enumerable.Range(0, 5)
            .Select(i => CreateCitation(i, 1f - (i * 0.05f), $"chunk text {i}"))
            .ToList();

        ContextPackResult result = packer.Pack(
            citations,
            maxContextTokens: 4096,
            generationReserveTokens: 512,
            historyTokenEstimate: 0,
            ContextPackingStrategy.LostInTheMiddle);

        foreach (var packed in result.PackedCitations)
        {
            Citation original = citations.Single(c => c.ChunkId == packed.ChunkId);
            Assert.Equal(original.ChunkIndex, packed.ChunkIndex);
            Assert.Equal(original.RankScore, packed.RankScore);
        }
    }
}
