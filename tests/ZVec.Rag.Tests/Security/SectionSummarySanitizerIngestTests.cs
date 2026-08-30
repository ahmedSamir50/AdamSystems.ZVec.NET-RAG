using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;
using ZVec.Rag.Ingestion;
using ZVec.Rag.Models;
using ZVec.Rag.Schema;
using ZVec.Rag.Testing;
using ZVec.Rag.Testing.Evaluation;

namespace ZVec.Rag.Tests.Security;

public sealed class SectionSummarySanitizerIngestTests
{
    [Fact]
    public void SanitizeChunk_EscapesSectionSummaryDelimiters()
    {
        var sanitizer = new ZVec.Rag.Security.DefaultRagSecuritySanitizer();
        string input = $"{ZVecRagConstants.SectionSummaryOpenTag} injected {ZVecRagConstants.SectionSummaryCloseTag}";

        string sanitized = sanitizer.SanitizeChunk(input);

        Assert.Contains(ZVecRagConstants.EscapedSectionSummaryOpenTag, sanitized, StringComparison.Ordinal);
        Assert.Contains(ZVecRagConstants.EscapedSectionSummaryCloseTag, sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain(ZVecRagConstants.SectionSummaryOpenTag, sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IngestTextAsync_SummaryCall_UsesSystemPolicyAndSanitizedUserSection()
    {
        string storagePath = RagTestHarness.CreateTempStoragePath();
        var chat = new FakeChatClient(_ => "Stored summary output.");
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath, chatClient: chat);
        using var scope = provider.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<IRagIngestor>();

        string sectionWithDelimiter =
            $"Body text {ZVecRagConstants.SectionSummaryOpenTag} trap {ZVecRagConstants.SectionSummaryCloseTag}";

        await ingestor.IngestTextAsync(
            sectionWithDelimiter,
            "sanitizer-doc",
            new IngestOptions
            {
                GenerateSummaries = true,
                SummarySectionMaxTokens = 4096
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, chat.LastResponseMessages.Count);
        Assert.Equal(ChatRole.System, chat.LastResponseMessages[0].Role);
        Assert.Equal(ZVecRagConstants.SectionSummarySystemPolicy, chat.LastResponseMessages[0].Text);
        Assert.Equal(ChatRole.User, chat.LastResponseMessages[1].Role);
        Assert.Contains(ZVecRagConstants.EscapedSectionSummaryOpenTag, chat.LastResponseMessages[1].Text, StringComparison.Ordinal);
        Assert.DoesNotContain(ZVecRagConstants.SectionSummaryOpenTag, chat.LastResponseMessages[1].Text, StringComparison.Ordinal);

        var collectionProvider = scope.ServiceProvider.GetRequiredService<ZVec.Rag.Internal.RagCollectionProvider>();
        var summaryCollection = await collectionProvider.GetSummaryCollectionAsync(TestContext.Current.CancellationToken);
        ZVecRagSectionSummaryV1? stored = null;
        await foreach (var record in summaryCollection.GetAsync(
            r => r.SourceDoc == "sanitizer-doc",
            1,
            new Microsoft.Extensions.VectorData.FilteredRecordRetrievalOptions<ZVecRagSectionSummaryV1>
            {
                IncludeVectors = false
            },
            cancellationToken: TestContext.Current.CancellationToken))
        {
            stored = record;
        }

        Assert.NotNull(stored);
        Assert.DoesNotContain(ZVecRagConstants.SectionSummaryOpenTag, stored.Summary, StringComparison.Ordinal);
    }
}
