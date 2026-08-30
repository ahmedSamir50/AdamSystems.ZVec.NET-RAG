using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.AI;
using ZVec.Rag.Constants;

namespace ZVec.Rag.Telemetry;

/// <summary>
/// OpenTelemetry-compatible activity and meter sources for the ZVec.Rag pipeline.
/// </summary>
public static class ZVecRagTelemetry
{
    /// <summary>Activity source for ingest, retrieve, and generate stages.</summary>
    public static readonly ActivitySource ActivitySource = new(ZVecRagConstants.TelemetrySourceName);

    /// <summary>Meter for token counters and stage duration histograms.</summary>
    public static readonly Meter Meter = new(ZVecRagConstants.TelemetrySourceName);

    private static readonly Counter<long> TokenCounter = Meter.CreateCounter<long>(ZVecRagConstants.MeterTokenCounterName);

    private static readonly Histogram<double> StageDurationHistogram =
        Meter.CreateHistogram<double>(ZVecRagConstants.MeterStageDurationHistogramName, unit: "ms");

    /// <summary>Records token usage for a pipeline stage.</summary>
    public static void RecordTokens(string stage, string direction, long count)
    {
        if (count <= 0)
        {
            return;
        }

        TokenCounter.Add(count, new KeyValuePair<string, object?>(ZVecRagConstants.TelemetryTagStage, stage),
            new KeyValuePair<string, object?>(ZVecRagConstants.TelemetryTagDirection, direction));
    }

    /// <summary>Records token usage from <see cref="UsageDetails"/> when present.</summary>
    public static void RecordUsageDetails(string stage, UsageDetails? usage)
    {
        if (usage?.InputTokenCount is long inputTokens)
        {
            RecordTokens(stage, ZVecRagConstants.TelemetryDirectionInput, inputTokens);
        }

        if (usage?.OutputTokenCount is long outputTokens)
        {
            RecordTokens(stage, ZVecRagConstants.TelemetryDirectionOutput, outputTokens);
        }
    }

    /// <summary>Records stage duration in milliseconds.</summary>
    public static void RecordStageDuration(string stage, double durationMs)
    {
        StageDurationHistogram.Record(durationMs, new KeyValuePair<string, object?>(ZVecRagConstants.TelemetryTagStage, stage));
    }
}
