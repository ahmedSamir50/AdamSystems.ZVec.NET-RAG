using System.Threading.Channels;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;
using ZVec.Rag.Ingestion;
using ZVec.Rag.Models;

namespace ZVec.Rag.Tests.Ingestion;

/// <summary>
/// Channel backpressure tests for ingestion (no native upsert).
/// </summary>
public sealed class IngestionChannelPumpTests
{
    [Fact]
    public async Task PumpChunksAsync_RecordsCallerThread_ForFirstChunks()
    {
        var channel = IngestionChannelPump.CreateParseChannel();
        int callerThreadId = Environment.CurrentManagedThreadId;
        var chunker = new ThreadRecordingChunker(8);

        int pumped = await IngestionChannelPump.PumpChunksAsync(
            chunker,
            "ignored",
            channel.Writer,
            TestContext.Current.CancellationToken);

        channel.Writer.Complete();
        Assert.Equal(8, pumped);
        Assert.All(chunker.RecordedThreadIds, id => Assert.Equal(callerThreadId, id));
    }

    [Fact]
    public async Task PumpChunksAsync_AppliesBackpressure_WhenConsumerIsSlow()
    {
        var channel = Channel.CreateBounded<TextChunk>(new BoundedChannelOptions(ZVecRagConstants.ParseChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });

        var reader = channel.Reader;
        var chunker = new SlowFakeChunker(2000);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        Task<int> pumpTask = IngestionChannelPump.PumpChunksAsync(
            chunker,
            "ignored",
            channel.Writer,
            cts.Token);

        int readCount = 0;
        while (!pumpTask.IsCompleted)
        {
            while (reader.TryRead(out _))
            {
                readCount++;
            }

            await Task.Delay(1, cts.Token);
        }

        while (reader.TryRead(out _))
        {
            readCount++;
        }

        int pumped = await pumpTask;
        Assert.Equal(2000, pumped);
        Assert.Equal(2000, readCount);
    }

    private sealed class ThreadRecordingChunker : IZVecTextChunker
    {
        private readonly int _count;

        public ThreadRecordingChunker(int count) => _count = count;

        public List<int> RecordedThreadIds { get; } = new();

        public string StrategyId => "thread-recording";

        public IEnumerable<TextChunk> Chunk(string text)
        {
            for (int i = 0; i < _count; i++)
            {
                RecordedThreadIds.Add(Environment.CurrentManagedThreadId);
                yield return new TextChunk($"chunk-{i}", i);
            }
        }
    }

    private sealed class SlowFakeChunker : IZVecTextChunker
    {
        private readonly int _count;

        public SlowFakeChunker(int count) => _count = count;

        public string StrategyId => "slow-fake";

        public IEnumerable<TextChunk> Chunk(string text)
        {
            for (int i = 0; i < _count; i++)
            {
                yield return new TextChunk($"chunk-{i}", i);
            }
        }
    }
}
