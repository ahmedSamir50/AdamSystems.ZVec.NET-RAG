using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ZVec.Extensions.VectorData.Store;
using ZVec.NET;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Ingestion;
using ZVec.Rag.Models;
using ZVec.Rag.Samples;
using ZVec.Rag.Testing;
using ZVec.Rag.Testing.Evaluation;

namespace ZVec.Rag.Tests.Samples;

public sealed class BackgroundCollectionOpenerTests
{
    [Fact]
    public async Task OpenAsync_InvokesCallbackOffUiSynchronizationContext()
    {
        var uiContext = new FlaggingSynchronizationContext();
        bool ranOnUi = false;

        await uiContext.Run(async () =>
        {
            await BackgroundCollectionOpener.OpenAsync(ct =>
            {
                if (SynchronizationContext.Current == uiContext)
                {
                    ranOnUi = true;
                }

                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);
        });

        Assert.False(ranOnUi);
    }

    private sealed class FlaggingSynchronizationContext : SynchronizationContext
    {
        public async Task Run(Func<Task> action)
        {
            var tcs = new TaskCompletionSource();
            Post(_ =>
            {
                SynchronizationContext.SetSynchronizationContext(this);
                action().ContinueWith(t => tcs.SetResult(), CancellationToken.None);
            }, null);
            await tcs.Task;
        }

        public override void Post(SendOrPostCallback d, object? state) => ThreadPool.QueueUserWorkItem(_ => d(state));
    }
}
