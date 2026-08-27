using Microsoft.Extensions.AI;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;
using ZVec.Rag.Models;
using ZVec.Rag.Options;
using ZVec.Rag.Retrieval;
using ZVec.Rag.Security;

namespace ZVec.Rag.Generation;

/// <summary>
/// Retrieves context, packs a token budget, and streams answers from <see cref="IChatClient"/>.
/// </summary>
public sealed class RagGenerator : IRagGenerator
{
    private readonly IRagRetriever _retriever;
    private readonly ZVecRagOptions _ragOptions;
    private readonly ContextPacker _contextPacker;
    private readonly IRagSecuritySanitizer _securitySanitizer;

    /// <summary>Initializes a new instance.</summary>
    public RagGenerator(
        IRagRetriever retriever,
        ZVecRagOptions ragOptions,
        ContextPacker? contextPacker = null,
        IRagSecuritySanitizer? securitySanitizer = null)
    {
        _retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
        _ragOptions = ragOptions ?? throw new ArgumentNullException(nameof(ragOptions));
        _contextPacker = contextPacker ?? new ContextPacker();
        _securitySanitizer = securitySanitizer
            ?? ragOptions.SecuritySanitizer
            ?? new DefaultRagSecuritySanitizer();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<RagChunk> AskAsync(
        string question,
        IList<ChatMessage>? history = null,
        bool streamCitations = true,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException(ZVecRagErrorMessages.NullOrEmptyQuestion(), nameof(question));
        }

        var chat = _ragOptions.Chat
            ?? throw new InvalidOperationException(ZVecRagErrorMessages.ChatClientNotConfigured());

        IReadOnlyList<Citation> retrieved = await _retriever.RetrieveAsync(
            question,
            _ragOptions.RetrieveTopK,
            cancellationToken).ConfigureAwait(false);

        IReadOnlyList<Citation> sanitized = SanitizeCitations(retrieved);
        IReadOnlyList<Citation> citationList = RagRetriever.SortCitations(sanitized, _ragOptions.CitationOrder);
        int historyTokens = _contextPacker.EstimateHistoryTokens(history);

        ContextPackResult packed = _contextPacker.Pack(
            citationList,
            _ragOptions.MaxContextTokens,
            _ragOptions.GenerationReserveTokens,
            historyTokens,
            _ragOptions.ContextPacking);

        var messages = BuildMessages(question, history, packed.ContextBlock);
        IReadOnlyList<Citation> streamCitationsList = streamCitations ? citationList : Array.Empty<Citation>();

        await foreach (var update in chat.GetStreamingResponseAsync(messages, cancellationToken: cancellationToken)
            .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string text = update.Text ?? string.Empty;
            bool isFinal = update.FinishReason != null;
            yield return new RagChunk(text, streamCitationsList, isFinal, update.Contents.OfType<UsageContent>().FirstOrDefault()?.Details);
        }
    }

    private IReadOnlyList<Citation> SanitizeCitations(IReadOnlyList<Citation> citations)
    {
        if (citations.Count == 0)
        {
            return citations;
        }

        var result = new List<Citation>(citations.Count);
        foreach (var citation in citations)
        {
            result.Add(citation with { Text = _securitySanitizer.SanitizeChunk(citation.Text) });
        }

        return result;
    }

    private static List<ChatMessage> BuildMessages(string question, IList<ChatMessage>? history, string contextBlock)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, ZVecRagConstants.RagSystemPolicy),
        };

        if (!string.IsNullOrWhiteSpace(contextBlock))
        {
            messages.Add(new ChatMessage(ChatRole.User, contextBlock));
        }

        if (history != null)
        {
            messages.AddRange(history);
        }

        messages.Add(new ChatMessage(ChatRole.User, question));
        return messages;
    }
}
