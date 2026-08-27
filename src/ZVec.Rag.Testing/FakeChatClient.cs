using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace ZVec.Rag.Testing;

/// <summary>
/// Configurable dual streaming/non-streaming chat client for RAG pipeline tests.
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    private readonly IReadOnlyList<string> _tokens;
    private readonly TimeSpan _delayPerToken;

    /// <summary>Initializes a new instance with a token sequence.</summary>
    public FakeChatClient(params string[] tokens)
        : this(tokens, TimeSpan.Zero)
    {
    }

    /// <summary>Initializes a new instance with a token sequence and per-token delay.</summary>
    public FakeChatClient(IReadOnlyList<string> tokens, TimeSpan delayPerToken)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _delayPerToken = delayPerToken;
    }

    /// <summary>Gets the number of streaming calls observed.</summary>
    public int StreamingCallCount { get; private set; }

    /// <summary>Gets the number of tokens yielded in the current or last streaming call.</summary>
    public int TokensYielded { get; private set; }

    /// <summary>Gets whether the last streaming call received a canceled token.</summary>
    public bool LastStreamingCallWasCanceled { get; private set; }

    /// <inheritdoc />
    public ChatClientMetadata Metadata { get; } = new("fake-chat-client");

    /// <inheritdoc />
    public TService? GetService<TService>(object? key = null) where TService : class => null;

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc />
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string combined = string.Concat(_tokens);
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, combined)));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        StreamingCallCount++;
        TokensYielded = 0;

        for (int i = 0; i < _tokens.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                LastStreamingCallWasCanceled = true;
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (_delayPerToken > TimeSpan.Zero)
            {
                await Task.Delay(_delayPerToken, cancellationToken).ConfigureAwait(false);
            }

            bool isLast = i == _tokens.Count - 1;
            TokensYielded++;
            yield return new ChatResponseUpdate(ChatRole.Assistant, _tokens[i])
            {
                FinishReason = isLast ? ChatFinishReason.Stop : null
            };
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
