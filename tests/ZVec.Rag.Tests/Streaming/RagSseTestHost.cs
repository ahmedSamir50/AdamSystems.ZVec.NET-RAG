using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZVec.Rag.Schema;
using ZVec.Rag.Streaming;
using ZVec.Rag.Testing;

namespace ZVec.Rag.Tests.Streaming;

/// <summary>
/// In-memory test host for SSE endpoint tests.
/// </summary>
public sealed class RagSseTestHost : IAsyncDisposable
{
  /// <summary>Gets the fake chat client wired into the host.</summary>
    public FakeChatClient ChatClient { get; } = new(["token1", "token2", "token3", "token4"], TimeSpan.FromMilliseconds(200));

    /// <summary>Gets isolated storage for this host instance.</summary>
    public string StoragePath { get; } = RagTestHarness.CreateTempStoragePath();

    private readonly IHost _host;

    /// <summary>Gets the host service provider for scoped pipeline access.</summary>
    public IServiceProvider Services => _host.Services;

    /// <summary>Initializes the in-memory SSE test host.</summary>
    public RagSseTestHost()
    {
        _host = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddZVecRag(opts =>
                    {
                        opts.StoragePath = StoragePath;
                        opts.Embedder = new DeterministicEmbedder(ZVecRagRecordV1.DefaultDimensions);
                        opts.Chat = ChatClient;
                        opts.VectorStore.ModelId = "test-model-v1";
                    })
                    .AddTokenChunker();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapRagSseEndpoint("/chat");
                    });
                });
            })
            .Start();
    }

    /// <summary>Creates an HTTP client for the test host.</summary>
    public HttpClient CreateClient() => _host.GetTestClient();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _host.Dispose();
        await Task.CompletedTask;
    }
}
