using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;
using ZVec.Rag.Ingestion;
using ZVec.Rag.Models;

namespace ZVec.Rag.Tests.Ingestion;

/// <summary>
/// Integration tests for <see cref="RagIngestor"/>.
/// </summary>
public sealed class RagIngestorTests
{
    [Fact]
    public async Task IngestTextAsync_LongText_ProducesMultipleChunks()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath);
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();

        string text = string.Join(' ', Enumerable.Range(0, 400).Select(i => $"word{i}"));
        var result = await ingestor.IngestTextAsync(text, "long-doc", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.ChunksIngested > 1);
        Assert.Equal(result.ChunksIngested, result.ChunkIds.Count);
    }

    [Fact]
    public async Task IngestTextAsync_OnDuplicateReplace_DeletesOldChunks()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath);
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();

        string longText = string.Join(' ', Enumerable.Range(0, 400).Select(i => $"word{i}"));
        var first = await ingestor.IngestTextAsync(longText, "doc-replace", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(first.ChunksIngested > 1);

        var replaced = await ingestor.IngestTextAsync(
            "short replacement",
            "doc-replace",
            new IngestOptions { OnDuplicate = DuplicateMode.Replace },
            TestContext.Current.CancellationToken);

        Assert.Equal(1, replaced.ChunksIngested);
        Assert.DoesNotContain(first.ChunkIds[^1], replaced.ChunkIds);
    }

    [Fact]
    public async Task IngestTextAsync_OnDuplicateSkip_ReturnsZeroChunks()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath);
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();

        await ingestor.IngestTextAsync("seed", "doc-skip", cancellationToken: TestContext.Current.CancellationToken);
        var skipped = await ingestor.IngestTextAsync(
            "new",
            "doc-skip",
            new IngestOptions { OnDuplicate = DuplicateMode.Skip },
            TestContext.Current.CancellationToken);

        Assert.Equal(0, skipped.ChunksIngested);
    }

    [Fact]
    public async Task IngestTextAsync_ThrowsOperationCanceledException_WhenCanceledMidIngest()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath);
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await ingestor.IngestTextAsync("cancel me", "doc", cancellationToken: cts.Token));
    }

    [Fact]
    public async Task IngestDocumentAsync_Markdown_UsesHeadingChunker()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath);
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();

        string md = "# Intro\n\nParagraph one.\n\n## Details\n\nParagraph two.";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(md));
        var result = await ingestor.IngestDocumentAsync(
            stream,
            "md-doc",
            ZVecRagConstants.MarkdownContentType,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.ChunksIngested >= 2);
        string expectedId = ZVecChunkIdGenerator.Compute(
            "md-doc",
            ZVecRagConstants.MarkdownHeadingChunkerStrategyId,
            0);
        Assert.Equal(expectedId, result.ChunkIds[0]);
    }
}
