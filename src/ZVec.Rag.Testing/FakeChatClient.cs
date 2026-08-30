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
    private readonly Func<IReadOnlyList<ChatMessage>, string>? _responseFactory;
    private readonly UsageDetails? _streamingUsage;

    /// <summary>Initializes a new instance with a token sequence.</summary>
    public FakeChatClient(params string[] tokens)
        : this(tokens, TimeSpan.Zero)
    {
    }

    /// <summary>Initializes a new instance with a token sequence and per-token delay.</summary>
    public FakeChatClient(IReadOnlyList<string> tokens, TimeSpan delayPerToken)
        : this(tokens, delayPerToken, streamingUsage: null)
    {
    }

    /// <summary>Initializes a new instance with optional usage on the final streaming update.</summary>
    public FakeChatClient(IReadOnlyList<string> tokens, TimeSpan delayPerToken, UsageDetails? streamingUsage)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _delayPerToken = delayPerToken;
        _streamingUsage = streamingUsage;
    }

    /// <summary>Initializes a new instance with a custom non-streaming response factory.</summary>
    public FakeChatClient(Func<IReadOnlyList<ChatMessage>, string> responseFactory)
    {
        _responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
        _tokens = Array.Empty<string>();
        _delayPerToken = TimeSpan.Zero;
        _streamingUsage = null;
    }

    /// <summary>Gets the number of streaming calls observed.</summary>
    public int StreamingCallCount { get; private set; }

    /// <summary>Gets the number of tokens yielded in the current or last streaming call.</summary>
    public int TokensYielded { get; private set; }

    /// <summary>Gets whether the last streaming call received a canceled token.</summary>
    public bool LastStreamingCallWasCanceled { get; private set; }

    /// <summary>Gets messages from the last streaming call.</summary>
    public IReadOnlyList<ChatMessage> LastStreamingMessages { get; private set; } = Array.Empty<ChatMessage>();

    /// <summary>Gets messages from the last non-streaming call.</summary>
    public IReadOnlyList<ChatMessage> LastResponseMessages { get; private set; } = Array.Empty<ChatMessage>();

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
        LastResponseMessages = messages.ToList();
        string combined = _responseFactory != null
            ? _responseFactory(LastResponseMessages)
            : string.Concat(_tokens);
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
        LastStreamingMessages = messages.ToList();

        for (int i = 0; i < _tokens.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                LastStreamingCallWasCanceled = true;
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (_delayPerToken > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(_delayPerToken, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    LastStreamingCallWasCanceled = true;
                    throw;
                }
            }

            bool isLast = i == _tokens.Count - 1;
            TokensYielded++;
            if (isLast && _streamingUsage != null)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, _tokens[i])
                {
                    FinishReason = ChatFinishReason.Stop,
                    Contents = [new TextContent(_tokens[i]), new UsageContent(_streamingUsage)]
                };
            }
            else
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, _tokens[i])
                {
                    FinishReason = isLast ? ChatFinishReason.Stop : null
                };
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
