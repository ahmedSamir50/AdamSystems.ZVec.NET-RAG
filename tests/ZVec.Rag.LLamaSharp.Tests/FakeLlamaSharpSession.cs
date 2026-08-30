using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using ZVec.Rag.LLamaSharp;

namespace ZVec.Rag.LLamaSharp.Tests;

internal sealed class FakeLlamaSharpSession : ILlamaSharpSession
{
    private readonly IReadOnlyList<string> _tokens;
    private readonly int _dimensions;
    private readonly TimeSpan _delayPerToken;
    private bool _disposed;

    public FakeLlamaSharpSession(IReadOnlyList<string> tokens, int dimensions = 768, TimeSpan? delayPerToken = null)
    {
        _tokens = tokens;
        _dimensions = dimensions;
        _delayPerToken = delayPerToken ?? TimeSpan.Zero;
    }

    public async IAsyncEnumerable<string> GenerateStreamingAsync(
        IReadOnlyList<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        for (int i = 0; i < _tokens.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_delayPerToken > TimeSpan.Zero)
            {
                await Task.Delay(_delayPerToken, cancellationToken).ConfigureAwait(false);
            }

            yield return _tokens[i];
        }
    }

    public ValueTask<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(LLamaSharpErrorMessages.EmptyEmbedInput(), nameof(text));
        }

        var vector = new float[_dimensions];
        vector[0] = 1f;
        return ValueTask.FromResult<ReadOnlyMemory<float>>(vector);
    }

    public void Dispose() => _disposed = true;
}
