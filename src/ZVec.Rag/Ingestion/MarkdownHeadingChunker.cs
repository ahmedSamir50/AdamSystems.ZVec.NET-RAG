using System.Text.RegularExpressions;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;
using ZVec.Rag.Models;

namespace ZVec.Rag.Ingestion;

/// <summary>
/// Splits markdown on heading lines while respecting token limits per section.
/// </summary>
public sealed class MarkdownHeadingChunker : IZVecTextChunker
{
    private readonly TokenTextChunker _tokenChunker;

    /// <summary>Initializes a new instance wrapping a token chunker for oversized sections.</summary>
    public MarkdownHeadingChunker(TokenTextChunker tokenChunker)
    {
        _tokenChunker = tokenChunker ?? throw new ArgumentNullException(nameof(tokenChunker));
    }

    /// <inheritdoc />
    public string StrategyId => ZVecRagConstants.MarkdownHeadingChunkerStrategyId;

    /// <inheritdoc />
    public IEnumerable<TextChunk> Chunk(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<TextChunk>();
        }

        var sections = SplitSections(text);
        var results = new List<TextChunk>();

        foreach (var (sectionText, sectionOffset) in sections)
        {
            if (string.IsNullOrWhiteSpace(sectionText))
            {
                continue;
            }

            foreach (var chunk in _tokenChunker.Chunk(sectionText))
            {
                results.Add(new TextChunk(chunk.Text, sectionOffset + chunk.Offset));
            }
        }

        return results;
    }

    private static List<(string Text, long Offset)> SplitSections(string text)
    {
        var sections = new List<(string, long)>();
        var matches = Regex.Matches(text, @"(?m)^#{1,6}\s+.*$");
        if (matches.Count == 0)
        {
            sections.Add((text, 0));
            return sections;
        }

        int previousEnd = 0;
        for (int i = 0; i < matches.Count; i++)
        {
            int headingStart = matches[i].Index;
            if (headingStart > previousEnd)
            {
                sections.Add((text.Substring(previousEnd, headingStart - previousEnd), previousEnd));
            }

            int nextStart = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            sections.Add((text.Substring(headingStart, nextStart - headingStart), headingStart));
            previousEnd = nextStart;
        }

        if (previousEnd < text.Length)
        {
            sections.Add((text.Substring(previousEnd), previousEnd));
        }

        return sections;
    }
}
