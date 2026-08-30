using Microsoft.Extensions.AI;
using ZVec.Rag.Testing;

namespace ZVec.Rag.Tests.Testing;

/// <summary>
/// Unit tests for <see cref="FakeChatClient"/>.
/// </summary>
public sealed class FakeChatClientTests
{
    [Fact]
    public async Task GetResponseAsync_ReturnsConcatenatedTokens()
    {
        var client = new FakeChatClient("Hello", " ", "World");
        ChatResponse response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "test")],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Hello World", response.Text);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_YieldsEachToken_WithFinalFinishReason()
    {
        var client = new FakeChatClient("A", "B", "C");
        var tokens = new List<string>();

        await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "test")],
            cancellationToken: TestContext.Current.CancellationToken))
        {
            tokens.Add(update.Text ?? string.Empty);
        }

        Assert.Equal(["A", "B", "C"], tokens);
        Assert.Equal(1, client.StreamingCallCount);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_IncludesUsageContent_WhenConfigured()
    {
        var usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 };
        var client = new FakeChatClient(["Done"], TimeSpan.Zero, usage);
        ChatResponseUpdate? finalUpdate = null;

        await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "test")],
            cancellationToken: TestContext.Current.CancellationToken))
        {
            finalUpdate = update;
        }

        Assert.NotNull(finalUpdate);
        Assert.Equal("Done", finalUpdate.Text);
        UsageContent? usageContent = finalUpdate.Contents.OfType<UsageContent>().FirstOrDefault();
        Assert.NotNull(usageContent);
        Assert.Equal(10, usageContent.Details.InputTokenCount);
        Assert.Equal(5, usageContent.Details.OutputTokenCount);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_ThrowsOperationCanceledException_WhenCanceled()
    {
        var client = new FakeChatClient(["slow", "tokens"], TimeSpan.FromSeconds(30));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "test")],
                cancellationToken: cts.Token))
            {
            }
        });

        Assert.True(client.LastStreamingCallWasCanceled);
    }
}
