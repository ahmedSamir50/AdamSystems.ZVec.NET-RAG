using Microsoft.Extensions.AI;
using ZVec.Rag.LLamaSharp;

namespace ZVec.Rag.LLamaSharp.Tests;

public sealed class LLamaSharpChatClientTests
{
    [Fact]
    public async Task GetStreamingResponseAsync_ConcatenatesTokens()
    {
        using var client = new LLamaSharpChatClient(new FakeLlamaSharpSession(["Hello", " ", "World"]));
        var tokens = new List<string>();
        await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "Hi")],
            cancellationToken: TestContext.Current.CancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                tokens.Add(update.Text!);
            }
        }

        Assert.Equal(["Hello", " ", "World"], tokens);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_CancellationMidStream_ThrowsOperationCanceledException()
    {
        using var client = new LLamaSharpChatClient(
            new FakeLlamaSharpSession(["a", "b", "c"], delayPerToken: TimeSpan.FromMilliseconds(50)));
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(10);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (ChatResponseUpdate _ in client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Hi")],
                cancellationToken: cts.Token))
            {
            }
        });
    }

    [Fact]
    public async Task GetResponseAsync_JoinsTokens()
    {
        using var client = new LLamaSharpChatClient(new FakeLlamaSharpSession(["A", "B"]));
        ChatResponse response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Hi")],
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("AB", response.Text);
    }

    [Fact]
    public void Constructor_NullSession_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new LLamaSharpChatClient(null!));
    }

    [Fact]
    public void Constructor_EmptyModelPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new LLamaSharpChatClient(new LLamaSharpOptions()));
    }

    [Fact]
    public async Task Dispose_ThenGenerate_ThrowsObjectDisposedException()
    {
        var client = new LLamaSharpChatClient(new FakeLlamaSharpSession(["x"]), ownsSession: true);
        client.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "Hi")],
                cancellationToken: TestContext.Current.CancellationToken));
    }
}
