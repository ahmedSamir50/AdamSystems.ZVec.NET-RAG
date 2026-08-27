using System.Text.RegularExpressions;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;
using ZVec.Rag.Models;

namespace ZVec.Rag.Ingestion;

/// <summary>
/// Splits text on sentence boundaries to avoid mid-sentence cuts.
/// </summary>
public sealed class SentenceTextChunker : IZVecTextChunker
{
    private static readonly Regex SentenceBoundary = new(
        @"(?<=[.!?])\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public string StrategyId => ZVecRagConstants.SentenceChunkerStrategyId;

    /// <inheritdoc />
    public IEnumerable<TextChunk> Chunk(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<TextChunk>();
        }

        var parts = SentenceBoundary.Split(text);
        var results = new List<TextChunk>();
        long offset = 0;

        foreach (string part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                offset += part.Length;
                continue;
            }

            int index = text.IndexOf(part, (int)Math.Min(offset, int.MaxValue), StringComparison.Ordinal);
            long chunkOffset = index >= 0 ? index : offset;
            results.Add(new TextChunk(part.Trim(), chunkOffset));
            offset = chunkOffset + part.Length;
        }

        if (results.Count == 0)
        {
            results.Add(new TextChunk(text, 0));
        }

        return results;
    }
}
