using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;
using ZVec.Rag.Telemetry;
using ZVec.Rag.Testing;

namespace ZVec.Rag.Tests.Telemetry;

/// <summary>
/// Tests for OpenTelemetry-compatible activity and meter instrumentation on the RAG pipeline.
/// </summary>
public sealed class ZVecRagTelemetryTests
{
    [Fact]
    public async Task Pipeline_EmitsActivitiesAndStageDurations_ForIngestRetrieveGenerate()
    {
        var activityNames = new ConcurrentBag<string>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ZVecRagConstants.TelemetrySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => activityNames.Add(activity.OperationName),
        };
        ActivitySource.AddActivityListener(activityListener);

        var ingestDurations = new ConcurrentBag<double>();
        var retrieveDurations = new ConcurrentBag<double>();
        var generateDurations = new ConcurrentBag<double>();
        long tokenCount = 0;

        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ZVecRagConstants.TelemetrySourceName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };

        meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name != ZVecRagConstants.MeterStageDurationHistogramName)
            {
                return;
            }

            string? stage = GetTag(tags, ZVecRagConstants.TelemetryTagStage);
            switch (stage)
            {
                case ZVecRagConstants.TelemetryStageIngest:
                    ingestDurations.Add(measurement);
                    break;
                case ZVecRagConstants.TelemetryStageRetrieve:
                    retrieveDurations.Add(measurement);
                    break;
                case ZVecRagConstants.TelemetryStageGenerate:
                    generateDurations.Add(measurement);
                    break;
            }
        });

        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == ZVecRagConstants.MeterTokenCounterName)
            {
                Interlocked.Add(ref tokenCount, measurement);
            }
        });

        meterListener.Start();

        string storagePath = RagTestHarness.CreateTempStoragePath();
        var chat = new FakeChatClient("Answer", " token");
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath, chatClient: chat);
        using var scope = provider.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IRagPipeline>();

        await pipeline.IngestTextAsync(
            "Telemetry test document about local vector search.",
            "telemetry-doc",
            cancellationToken: TestContext.Current.CancellationToken);

        IReadOnlyList<ZVec.Rag.Models.Citation> citations = await pipeline.RetrieveAsync(
            "local vector search",
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEmpty(citations);

        await foreach (var _ in pipeline.AskAsync(
            "What is this about?",
            cancellationToken: TestContext.Current.CancellationToken))
        {
        }

        meterListener.RecordObservableInstruments();

        Assert.Contains(ZVecRagConstants.ActivityNameIngest, activityNames);
        Assert.Contains(ZVecRagConstants.ActivityNameRetrieve, activityNames);
        Assert.Contains(ZVecRagConstants.ActivityNameGenerate, activityNames);
        Assert.NotEmpty(ingestDurations);
        Assert.NotEmpty(retrieveDurations);
        Assert.NotEmpty(generateDurations);
        Assert.Equal(0, tokenCount);
    }

    [Fact]
    public async Task AskAsync_IncrementsTokenCounter_WhenFakeChatClientProvidesUsage()
    {
        long inputTokens = 0;
        long outputTokens = 0;

        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ZVecRagConstants.TelemetrySourceName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };

        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name != ZVecRagConstants.MeterTokenCounterName)
            {
                return;
            }

            string? stage = GetTag(tags, ZVecRagConstants.TelemetryTagStage);
            string? direction = GetTag(tags, ZVecRagConstants.TelemetryTagDirection);
            if (stage != ZVecRagConstants.TelemetryStageChat)
            {
                return;
            }

            if (direction == ZVecRagConstants.TelemetryDirectionInput)
            {
                Interlocked.Add(ref inputTokens, measurement);
            }
            else if (direction == ZVecRagConstants.TelemetryDirectionOutput)
            {
                Interlocked.Add(ref outputTokens, measurement);
            }
        });

        meterListener.Start();

        var usage = new UsageDetails
        {
            InputTokenCount = 42,
            OutputTokenCount = 7,
        };
        var chat = new FakeChatClient(["Done"], TimeSpan.Zero, usage);

        string storagePath = RagTestHarness.CreateTempStoragePath();
        await using var provider = RagTestHarness.CreateServiceProvider(storagePath, chatClient: chat);
        using var scope = provider.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IRagPipeline>();

        await pipeline.IngestTextAsync(
            "Usage telemetry document.",
            "usage-doc",
            cancellationToken: TestContext.Current.CancellationToken);

        await foreach (var _ in pipeline.AskAsync(
            "Summarize",
            cancellationToken: TestContext.Current.CancellationToken))
        {
        }

        meterListener.RecordObservableInstruments();

        Assert.Equal(42, inputTokens);
        Assert.Equal(7, outputTokens);
    }

    private static string? GetTag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string key)
    {
        foreach (KeyValuePair<string, object?> tag in tags)
        {
            if (tag.Key == key)
            {
                return tag.Value?.ToString();
            }
        }

        return null;
    }
}
