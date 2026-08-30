using Microsoft.Extensions.DependencyInjection;
using ZVec.Extensions.VectorData.Store;
using ZVec.NET;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Testing;

namespace ZVecRagApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();

        builder.Services.AddZVecRag(opts =>
        {
            opts.StoragePath = Path.Combine(FileSystem.AppDataDirectory, "rag.zvec");
            opts.Embedder = new DeterministicEmbedder();
            // Test double for CI and first run. Concatenates tokens; does not call a model. Replace with your IChatClient (Story 4.1).
            opts.Chat = new FakeChatClient("Hello", " from ZVec.Rag");
            opts.VectorStore.EnableMmap = true;
            opts.VectorStore.ReadOnly = true;
            opts.VectorStore.DefaultQuantizeType = ZVecQuantizeType.Fp16;
        })
        .AddTokenChunker();

        return builder.Build();
    }
}
