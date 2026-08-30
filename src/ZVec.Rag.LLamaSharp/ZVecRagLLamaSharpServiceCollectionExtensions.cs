using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ZVec.Rag.LLamaSharp;
using ZVec.Rag.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency injection extensions for ZVec.Rag LLamaSharp recipe.
/// </summary>
public static class ZVecRagLLamaSharpServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="LLamaSharpChatClient"/> and <see cref="LLamaSharpEmbedder"/> as singletons.
    /// When <see cref="ZVecRagOptions"/> is registered, sets <c>Chat</c> and <c>Embedder</c> when null.
    /// </summary>
    [RequiresUnreferencedCode("LLamaSharp native GGUF loading is not trim-safe for Native AOT.")]
    public static IServiceCollection AddZVecRagLLamaSharp(
        this IServiceCollection services,
        Action<LLamaSharpOptions> configure)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var options = new LLamaSharpOptions();
        configure(options);

        if (string.IsNullOrWhiteSpace(options.ModelPath))
        {
            throw new ArgumentException(LLamaSharpErrorMessages.ModelPathRequired(), nameof(configure));
        }

        services.TryAddSingleton(options);
        services.TryAddSingleton<ILlamaSharpSessionFactory, LlamaSharpNativeSessionFactory>();
        services.TryAddSingleton<ILlamaSharpSession>(sp =>
        {
            ILlamaSharpSessionFactory factory = sp.GetRequiredService<ILlamaSharpSessionFactory>();
            LLamaSharpOptions opts = sp.GetRequiredService<LLamaSharpOptions>();
            return factory.Create(opts);
        });

        services.TryAddSingleton(sp =>
        {
            ILlamaSharpSession session = sp.GetRequiredService<ILlamaSharpSession>();
            var chat = new LLamaSharpChatClient(session);
            if (sp.GetService<ZVecRagOptions>() is { } ragOptions)
            {
                ragOptions.Chat ??= chat;
            }

            return chat;
        });

        services.TryAddSingleton(sp =>
        {
            ILlamaSharpSession session = sp.GetRequiredService<ILlamaSharpSession>();
            var embedder = new LLamaSharpEmbedder(session);
            if (sp.GetService<ZVecRagOptions>() is { } ragOptions)
            {
                ragOptions.Embedder ??= embedder;
            }

            return embedder;
        });

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, LlamaSharpRagOptionsHostedService>());

        return services;
    }

    /// <summary>Ensures <see cref="ZVecRagOptions"/> is wired when the host starts.</summary>
    internal sealed class LlamaSharpRagOptionsHostedService : IHostedService
    {
        private readonly ZVecRagOptions? _ragOptions;
        private readonly LLamaSharpChatClient _chatClient;
        private readonly LLamaSharpEmbedder _embedder;

        /// <summary>Initializes a new instance.</summary>
        public LlamaSharpRagOptionsHostedService(
            LLamaSharpChatClient chatClient,
            LLamaSharpEmbedder embedder,
            ZVecRagOptions? ragOptions = null)
        {
            _chatClient = chatClient;
            _embedder = embedder;
            _ragOptions = ragOptions;
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_ragOptions is not null)
            {
                _ragOptions.Chat ??= _chatClient;
                _ragOptions.Embedder ??= _embedder;
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
