using System.Text.Json;
using ZVec.Rag.Generation;
using ZVec.Rag.Models;
using ZVec.Rag.Retrieval;

namespace ZVec.Rag.Tests.Generation;

/// <summary>
/// Verify snapshot tests for prompt formatting and citation ordering (Story 2.4.3).
/// </summary>
public sealed class ContextPackerSnapshotTests
{
    private static readonly JsonSerializerOptions CitationJsonOptions = new()
    {
        WriteIndented = true,
    };

    private static Citation CreateCitation(int index, float score, string text, string chunkId)
    {
        return new Citation(
            $"doc-{index}",
            $"uri://doc-{index}",
            $"hash-{index}",
            index,
            index * 100L,
            index,
            chunkId,
            text,
            score,
            score,
            0.1f);
    }

    [Fact]
    public Task Pack_ScoreDescending_ProducesStableRetrievedContextSnapshot()
    {
        var packer = new ContextPacker();
        var citations = new[]
        {
            CreateCitation(0, 0.95f, "Nomic embed text teaches local-first RAG.", "nomic-chunk-aaa"),
            CreateCitation(1, 0.85f, "ZVec stores vectors with hybrid FTS.", "nomic-chunk-bbb"),
            CreateCitation(2, 0.75f, "Microsoft.Extensions.AI supplies IChatClient.", "nomic-chunk-ccc"),
        };

        ContextPackResult result = packer.Pack(
            citations,
            maxContextTokens: 4096,
            generationReserveTokens: 512,
            historyTokenEstimate: 0,
            ContextPackingStrategy.ScoreDescending);

        return Verifier.Verify(result.ContextBlock).UseFileName("cl100k-nomic-v1");
    }

    [Fact]
    public Task Pack_Litm_ProducesDifferentPromptOrder_FromScoreDescending()
    {
        var packer = new ContextPacker();
        var citations = Enumerable.Range(0, 5)
            .Select(i => CreateCitation(i, 1f - (i * 0.1f), $"Litm chunk body {i}.", $"litm-chunk-{i}"))
            .ToList();

        ContextPackResult scoreDesc = packer.Pack(
            citations,
            maxContextTokens: 4096,
            generationReserveTokens: 512,
            historyTokenEstimate: 0,
            ContextPackingStrategy.ScoreDescending);

        ContextPackResult litm = packer.Pack(
            citations,
            maxContextTokens: 4096,
            generationReserveTokens: 512,
            historyTokenEstimate: 0,
            ContextPackingStrategy.LostInTheMiddle);

        Assert.NotEqual(scoreDesc.ContextBlock, litm.ContextBlock);
        Assert.Equal(
            citations.Select(c => c.ChunkId).OrderBy(id => id),
            litm.PackedCitations.Select(c => c.ChunkId).OrderBy(id => id));

        return Verifier.Verify(litm.ContextBlock).UseFileName("cl100k-nomic-v1-litm");
    }

    [Fact]
    public Task SortCitations_ScoreDescending_ProducesStableCitationIdentitySnapshot()
    {
        var citations = new[]
        {
            CreateCitation(2, 0.70f, "Third ranked chunk.", "cite-chunk-2"),
            CreateCitation(0, 0.90f, "Top ranked chunk.", "cite-chunk-0"),
            CreateCitation(1, 0.80f, "Middle ranked chunk.", "cite-chunk-1"),
        };

        IReadOnlyList<Citation> sorted = RagRetriever.SortCitations(citations, CitationOrder.ScoreDescending);
        string json = SerializeCitationIdentity(sorted);

        ContextPackResult litmPack = new ContextPacker().Pack(
            sorted,
            maxContextTokens: 4096,
            generationReserveTokens: 512,
            historyTokenEstimate: 0,
            ContextPackingStrategy.LostInTheMiddle);

        string litmJson = SerializeCitationIdentity(
            RagRetriever.SortCitations(litmPack.PackedCitations, CitationOrder.ScoreDescending));

        Assert.Equal(json, litmJson);

        return Verifier.Verify(json).UseFileName("citation-order-score-descending");
    }

    private static string SerializeCitationIdentity(IReadOnlyList<Citation> citations)
    {
        var rows = citations.Select(c => new
        {
            c.SourceDoc,
            c.ChunkIndex,
            c.ChunkId,
            c.RankScore,
            c.DenseScore,
            c.FtsScore,
        });

        return JsonSerializer.Serialize(rows, CitationJsonOptions);
    }
}
