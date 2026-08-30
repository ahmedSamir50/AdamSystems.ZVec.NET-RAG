using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.ONNX;
using ZVec.Rag.Options;
using ZVec.Rag.Schema;
using ZVec.Rag.Testing;
using static Microsoft.Extensions.DependencyInjection.ZVecRagOnnxServiceCollectionExtensions;

namespace ZVec.Rag.ONNX.Tests;

public sealed class AddZVecRagOnnxEmbedderTests
{
    [Fact]
    public void AddZVecRagOnnxEmbedder_768Dimensions_SetsEmbedderOnRagOptions()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOnnxSessionFactory>(new FakeOnnxSessionFactory());
        services.AddZVecRag(opts =>
        {
            opts.StoragePath = Path.Combine(Path.GetTempPath(), "onnx-test", Guid.NewGuid().ToString("N"));
            opts.Chat = new FakeChatClient("ok");
            opts.Embedder = null;
        });

        services.AddZVecRagOnnxEmbedder(o =>
        {
            o.ModelPath = "fake.onnx";
            o.ModelKind = OnnxEmbeddingModelKind.EmbeddingGemma;
            o.Dimensions = ZVecRagRecordV1.DefaultDimensions;
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<OnnxEmbedder>();
        ZVecRagOptions ragOptions = provider.GetRequiredService<ZVecRagOptions>();
        Assert.IsType<OnnxEmbedder>(ragOptions.Embedder);
    }

    [Fact]
    public void AddZVecRagOnnxEmbedder_384Dimensions_DoesNotSetEmbedderOnRagOptions()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOnnxSessionFactory>(new FakeOnnxSessionFactory());
        services.AddZVecRag(opts =>
        {
            opts.StoragePath = Path.Combine(Path.GetTempPath(), "onnx-test", Guid.NewGuid().ToString("N"));
            opts.Chat = new FakeChatClient("ok");
            opts.Embedder = null;
        });

        services.AddZVecRagOnnxEmbedder(o =>
        {
            o.ModelPath = "fake.onnx";
            o.ModelKind = OnnxEmbeddingModelKind.MiniLm;
            o.Dimensions = 384;
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<OnnxEmbedder>();
        ZVecRagOptions ragOptions = provider.GetRequiredService<ZVecRagOptions>();
        Assert.Null(ragOptions.Embedder);
    }

    private sealed class FakeOnnxSessionFactory : IOnnxSessionFactory
    {
        public IOnnxSession CreateTextSession(OnnxEmbedderOptions options) => new FakeOnnxSession(options.Dimensions);

        public IOnnxSession? CreateVisionSession(OnnxEmbedderOptions options)
            => options.ModelKind == OnnxEmbeddingModelKind.ClipText && !string.IsNullOrWhiteSpace(options.VisionModelPath)
                ? new FakeOnnxSession(OnnxConstants.ClipDimensions)
                : null;
    }
}
