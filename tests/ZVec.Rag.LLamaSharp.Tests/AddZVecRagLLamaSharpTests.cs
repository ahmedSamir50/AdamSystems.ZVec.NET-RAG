using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.LLamaSharp;
using ZVec.Rag.Options;
using ZVec.Rag.Testing;

namespace ZVec.Rag.LLamaSharp.Tests;

public sealed class AddZVecRagLLamaSharpTests
{
    [Fact]
    public void AddZVecRagLLamaSharp_NullServices_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddZVecRagLLamaSharp(_ => { }));
    }

    [Fact]
    public void AddZVecRagLLamaSharp_EmptyModelPath_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() =>
            services.AddZVecRagLLamaSharp(_ => { }));
    }

    [Fact]
    public void AddZVecRagLLamaSharp_AfterAddZVecRag_SetsChatWhenNull()
    {
        var services = new ServiceCollection();
        services.AddZVecRag(opts =>
        {
            opts.StoragePath = Path.Combine(Path.GetTempPath(), "llama-test", Guid.NewGuid().ToString("N"));
            opts.Embedder = new DeterministicEmbedder();
            opts.Chat = null;
        });

        services.AddSingleton<ILlamaSharpSessionFactory>(
            new FakeLlamaSharpSessionFactory(new FakeLlamaSharpSession(["ok"])));

        services.AddZVecRagLLamaSharp(o => o.ModelPath = "fake.gguf");

        using ServiceProvider provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<LLamaSharpChatClient>();
        ZVecRagOptions ragOptions = provider.GetRequiredService<ZVecRagOptions>();
        Assert.IsType<LLamaSharpChatClient>(ragOptions.Chat);
        Assert.IsType<DeterministicEmbedder>(ragOptions.Embedder);
    }

    private sealed class FakeLlamaSharpSessionFactory : ILlamaSharpSessionFactory
    {
        private readonly ILlamaSharpSession _session;

        public FakeLlamaSharpSessionFactory(ILlamaSharpSession session) => _session = session;

        public ILlamaSharpSession Create(LLamaSharpOptions options) => _session;
    }
}
