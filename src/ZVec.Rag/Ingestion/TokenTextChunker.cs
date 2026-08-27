using Microsoft.ML.Tokenizers;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;
using ZVec.Rag.Models;

namespace ZVec.Rag.Ingestion;

/// <summary>
/// Splits text on token boundaries using Tiktoken.
/// </summary>
public sealed class TokenTextChunker : IZVecTextChunker
{
    private readonly TiktokenTokenizer _tokenizer;
    private readonly int _maxTokens;
    private readonly int _overlapTokens;

    /// <summary>Initializes with default 512 max tokens and 64 overlap.</summary>
    public TokenTextChunker(TiktokenTokenizer tokenizer)
        : this(tokenizer, ZVecRagConstants.DefaultChunkMaxTokens, ZVecRagConstants.DefaultChunkOverlapTokens)
    {
    }

    /// <summary>Initializes with custom token limits.</summary>
    public TokenTextChunker(TiktokenTokenizer tokenizer, int maxTokens, int overlapTokens)
    {
        _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        if (maxTokens <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTokens));
        }

        if (overlapTokens < 0 || overlapTokens >= maxTokens)
        {
            throw new ArgumentOutOfRangeException(nameof(overlapTokens));
        }

        _maxTokens = maxTokens;
        _overlapTokens = overlapTokens;
    }

    /// <inheritdoc />
    public string StrategyId => ZVecRagConstants.TokenChunkerStrategyId;

    /// <inheritdoc />
    public IEnumerable<TextChunk> Chunk(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<TextChunk>();
        }

        return ChunkCore(text);
    }

    private IEnumerable<TextChunk> ChunkCore(string text)
    {
        IReadOnlyList<int> tokenIds = _tokenizer.EncodeToIds(text);
        if (tokenIds.Count == 0)
        {
            yield break;
        }

        int stride = _maxTokens - _overlapTokens;
        long searchFrom = 0;
        for (int startToken = 0; startToken < tokenIds.Count; startToken += stride)
        {
            int endToken = Math.Min(startToken + _maxTokens, tokenIds.Count);
            int[] slice = tokenIds.Skip(startToken).Take(endToken - startToken).ToArray();
            string chunkText = _tokenizer.Decode(slice);
            long offset = FindOffset(text, chunkText, searchFrom);
            searchFrom = offset + 1;
            yield return new TextChunk(chunkText, offset);

            if (endToken >= tokenIds.Count)
            {
                break;
            }
        }
    }

    private static long FindOffset(string fullText, string chunkText, long searchFrom)
    {
        if (string.IsNullOrEmpty(chunkText))
        {
            return searchFrom;
        }

        int index = fullText.IndexOf(chunkText, (int)Math.Min(searchFrom, int.MaxValue), StringComparison.Ordinal);
        return index >= 0 ? index : searchFrom;
    }
}
