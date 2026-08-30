using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace ZVec.Rag.LLamaSharp;

/// <summary>
/// <see cref="IChatClient"/> adapter over a local LLamaSharp GGUF model.
/// </summary>
[RequiresUnreferencedCode("LLamaSharp native GGUF loading is not trim-safe for Native AOT.")]
public sealed class LLamaSharpChatClient : IChatClient
{
    private readonly ILlamaSharpSession _session;
    private readonly bool _ownsSession;
    private bool _disposed;

    /// <summary>Initializes a new instance with the given options.</summary>
    public LLamaSharpChatClient(LLamaSharpOptions options)
        : this(new LlamaSharpNativeSession(options), ownsSession: true)
    {
    }

    /// <summary>Initializes a new instance with an existing session (for tests).</summary>
    internal LLamaSharpChatClient(ILlamaSharpSession session, bool ownsSession = false)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _ownsSession = ownsSession;
    }

    /// <inheritdoc />
    public ChatClientMetadata Metadata { get; } = new(LLamaSharpConstants.ChatClientModelId);

    /// <inheritdoc />
    public TService? GetService<TService>(object? key = null) where TService : class => null;

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var messageList = messages.ToList();
        var builder = new System.Text.StringBuilder();
        await foreach (string token in _session.GenerateStreamingAsync(messageList, cancellationToken)
            .ConfigureAwait(false))
        {
            builder.Append(token);
        }

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, builder.ToString()));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var messageList = messages.ToList();
        bool first = true;
        await foreach (string token in _session.GenerateStreamingAsync(messageList, cancellationToken)
            .ConfigureAwait(false))
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, token)
            {
                FinishReason = null
            };
            first = false;
        }

        if (!first)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, string.Empty)
            {
                FinishReason = ChatFinishReason.Stop
            };
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsSession)
        {
            _session.Dispose();
        }
    }
}
