using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Models;

namespace ZVec.Rag.Tests.Optimize;

/// <summary>
/// Tests for <see cref="IRagIngestor.OptimizeAsync"/>.
/// </summary>
public sealed class RagOptimizeTests
{
    [Fact]
    public async Task IngestBatchAsync_RunsOptimize_AndRetrieveStillWorks()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath);
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();
        var retriever = scope.ServiceProvider.GetRequiredService<IRagRetriever>();

        await ingestor.IngestBatchAsync(
            [
                new IngestTextRequest("First document about vectors.", "batch-1"),
                new IngestTextRequest("Second document about embeddings.", "batch-2")
            ],
            cancellationToken: TestContext.Current.CancellationToken);

        var citations = await retriever.RetrieveAsync("vectors embeddings", cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEmpty(citations);
    }

    [Fact]
    public async Task OptimizeAsync_ThrowsOperationCanceledException_WhenTokenCanceled()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath);
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await ingestor.OptimizeAsync(cts.Token));
    }

    [Fact]
    public async Task RetrieveAsync_DuringOptimizeAsync_DoesNotThrow()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath);
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();
        var retriever = scope.ServiceProvider.GetRequiredService<IRagRetriever>();

        await ingestor.IngestTextAsync(
            "Concurrent optimize and retrieve safety check.",
            "opt-doc",
            cancellationToken: TestContext.Current.CancellationToken);

        Task optimizeTask = ingestor.OptimizeAsync(TestContext.Current.CancellationToken);
        IReadOnlyList<Citation> citations = await retriever.RetrieveAsync(
            "concurrent optimize retrieve",
            cancellationToken: TestContext.Current.CancellationToken);
        await optimizeTask;

        Assert.NotEmpty(citations);
    }
}
