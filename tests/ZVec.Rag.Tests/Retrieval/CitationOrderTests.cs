using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Models;
using ZVec.Rag.Retrieval;

namespace ZVec.Rag.Tests.Retrieval;

/// <summary>
/// Unit tests for citation ordering.
/// </summary>
public sealed class CitationOrderTests
{
    private static readonly Citation[] Sample = [
        new("a", "a", "h1", 1, 0, 2, "id-a2", "text", 0.9f, 0f, 0f),
        new("b", "b", "h2", null, 10, 0, "id-b0", "text", 0.5f, 0f, 0f),
        new("a", "a", "h1", 2, 5, 0, "id-a0", "text", 0.7f, 0f, 0f)
    ];

    [Fact]
    public void SortCitations_ChunkOrderAscending_SortsByChunkIndex()
    {
        var sorted = RagRetriever.SortCitations(Sample, CitationOrder.ChunkOrderAscending);
        Assert.Equal(0, sorted[0].ChunkIndex);
        Assert.Equal(2, sorted[^1].ChunkIndex);
    }

    [Fact]
    public void SortCitations_SourceDocThenChunkOrder_GroupsByDoc()
    {
        var sorted = RagRetriever.SortCitations(Sample, CitationOrder.SourceDocThenChunkOrder);
        Assert.Equal("a", sorted[0].SourceDoc);
        Assert.Equal("b", sorted[^1].SourceDoc);
    }

    [Fact]
    public void SortCitations_PageAscending_PutsNullPageLast()
    {
        var sorted = RagRetriever.SortCitations(Sample, CitationOrder.PageAscending);
        Assert.Equal(1, sorted[0].Page);
        Assert.Null(sorted[^1].Page);
    }

    [Fact]
    public void SortCitations_None_PreservesOrder()
    {
        var sorted = RagRetriever.SortCitations(Sample, CitationOrder.None);
        Assert.Equal("id-a2", sorted[0].ChunkId);
    }
}
