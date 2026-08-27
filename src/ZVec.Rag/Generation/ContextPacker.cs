using Microsoft.ML.Tokenizers;
using ZVec.Rag.Constants;
using ZVec.Rag.Models;

namespace ZVec.Rag.Generation;

/// <summary>
/// Packs retrieved citations into a token-budgeted LLM context block.
/// Prompt packing order is independent of <see cref="CitationOrder"/>.
/// </summary>
public sealed class ContextPacker
{
    private readonly TiktokenTokenizer _tokenizer;

    /// <summary>Initializes a new instance with cl100k_base Tiktoken.</summary>
    public ContextPacker()
        : this(TiktokenTokenizer.CreateForEncoding(ZVecRagConstants.Cl100kBaseEncoding))
    {
    }

    /// <summary>Initializes a new instance with a custom tokenizer.</summary>
    public ContextPacker(TiktokenTokenizer tokenizer)
    {
        _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
    }

    /// <summary>
    /// Packs citations into a retrieved-context string respecting token budget and strategy.
    /// </summary>
    /// <param name="citations">Ranked citations (identity fields preserved regardless of packing order).</param>
    /// <param name="maxContextTokens">Maximum tokens for retrieved context.</param>
    /// <param name="generationReserveTokens">Tokens reserved for LLM output (subtracted from budget).</param>
    /// <param name="historyTokenEstimate">Estimated tokens consumed by chat history.</param>
    /// <param name="strategy">Context packing strategy (LITM permutes prompt order only).</param>
    /// <returns>Formatted context block and citations in packing order (not citation list order).</returns>
    public ContextPackResult Pack(
        IReadOnlyList<Citation> citations,
        int maxContextTokens,
        int generationReserveTokens,
        int historyTokenEstimate,
        ContextPackingStrategy strategy)
    {
        int available = maxContextTokens - generationReserveTokens - historyTokenEstimate;
        if (available <= 0)
        {
            return new ContextPackResult(string.Empty, Array.Empty<Citation>());
        }

        var selected = new List<Citation>();
        int usedTokens = 0;

        foreach (var citation in citations.OrderByDescending(c => c.RankScore))
        {
            int chunkTokens = CountTokens(citation.Text);
            if (usedTokens + chunkTokens > available)
            {
                continue;
            }

            selected.Add(citation);
            usedTokens += chunkTokens;
        }

        IReadOnlyList<Citation> packedOrder = strategy switch
        {
            ContextPackingStrategy.LostInTheMiddle => ApplyLostInTheMiddle(selected),
            _ => selected
        };

        string context = BuildContextBlock(packedOrder);
        return new ContextPackResult(context, packedOrder);
    }

    /// <summary>Estimates token count for chat history messages.</summary>
    public int EstimateHistoryTokens(IList<Microsoft.Extensions.AI.ChatMessage>? history)
    {
        if (history == null || history.Count == 0)
        {
            return 0;
        }

        int total = 0;
        foreach (var message in history)
        {
            foreach (var content in message.Contents)
            {
                if (content is Microsoft.Extensions.AI.TextContent textContent)
                {
                    total += CountTokens(textContent.Text);
                }
            }
        }

        return total;
    }

    private int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return _tokenizer.CountTokens(text);
    }

    private static string BuildContextBlock(IReadOnlyList<Citation> citations)
    {
        if (citations.Count == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        builder.AppendLine(ZVecRagConstants.RetrievedContextOpenTag);
        foreach (var citation in citations)
        {
            builder.Append("[chunk id=\"");
            builder.Append(citation.ChunkId);
            builder.Append("\"]\n");
            builder.AppendLine(citation.Text);
        }

        builder.AppendLine(ZVecRagConstants.RetrievedContextCloseTag);
        return builder.ToString();
    }

    /// <summary>
    /// Applies Lost-in-the-Middle reordering: best chunks at start and end, weaker in the middle.
    /// </summary>
    public static IReadOnlyList<Citation> ApplyLostInTheMiddle(IReadOnlyList<Citation> ranked)
    {
        if (ranked.Count <= 2)
        {
            return ranked;
        }

        var result = new Citation[ranked.Count];
        int left = 0;
        int right = ranked.Count - 1;
        bool placeLeft = true;

        foreach (var citation in ranked)
        {
            if (placeLeft)
            {
                result[left++] = citation;
            }
            else
            {
                result[right--] = citation;
            }

            placeLeft = !placeLeft;
        }

        return result;
    }
}

/// <summary>Result of context packing.</summary>
/// <param name="ContextBlock">Formatted retrieved context for the LLM prompt.</param>
/// <param name="PackedCitations">Citations in prompt order (identity fields unchanged).</param>
public sealed record ContextPackResult(string ContextBlock, IReadOnlyList<Citation> PackedCitations);
