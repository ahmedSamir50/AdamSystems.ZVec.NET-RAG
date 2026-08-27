using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;

namespace ZVec.Rag.Tests.Streaming;

/// <summary>
/// SSE endpoint integration tests (G2 cancellation).
/// </summary>
public sealed class RagSseEndpointTests : IAsyncDisposable
{
    private readonly RagSseTestHost _host = new();

    [Fact]
    public async Task MapRagSseEndpoint_StreamsUntilFinalChunk()
    {
        await SeedIngestAsync();
        using var client = _host.CreateClient();
        using var response = await client.GetAsync(
            "/chat?question=What+is+ZVec?",
            HttpCompletionOption.ResponseHeadersRead,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("token1", body, StringComparison.Ordinal);
        Assert.Contains("isFinal", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MapRagSseEndpoint_CancelsGeneration_WhenClientDisconnects()
    {
        await SeedIngestAsync();
        using var client = _host.CreateClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/chat?question=stream+cancel");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        using var stream = await response.Content.ReadAsStreamAsync(cts.Token);

        await stream.ReadAsync(new byte[1], cts.Token);
        await cts.CancelAsync();

        Assert.True(_host.ChatClient.LastStreamingCallWasCanceled || _host.ChatClient.TokensYielded < 4);
        Assert.True(_host.ChatClient.TokensYielded < 4);
    }

    private async Task SeedIngestAsync()
    {
        using var scope = _host.Services.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();
        await ingestor.IngestTextAsync(
            "ZVec is a local-first vector database for .NET applications.",
            "sse-seed",
            cancellationToken: TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
    }
}
