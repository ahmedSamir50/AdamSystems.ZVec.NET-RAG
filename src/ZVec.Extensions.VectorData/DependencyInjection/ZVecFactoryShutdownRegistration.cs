using Microsoft.Extensions.Hosting;
using ZVec.NET;

namespace ZVec.Extensions.VectorData.DependencyInjection;

/// <summary>
/// Registers <see cref="IZvecFactory.Shutdown"/> on <see cref="IHostApplicationLifetime.ApplicationStopping"/>.
/// </summary>
internal sealed class ZVecFactoryShutdownRegistration : IHostedService
{
    private readonly IZvecFactory _factory;
    private readonly IHostApplicationLifetime _lifetime;
    private CancellationTokenRegistration _stoppingRegistration;

    /// <summary>
    /// Initializes shutdown registration for the process-wide native factory.
    /// </summary>
    public ZVecFactoryShutdownRegistration(IZvecFactory factory, IHostApplicationLifetime lifetime)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stoppingRegistration = _lifetime.ApplicationStopping.Register(() =>
        {
            try
            {
                if (_factory.IsInitialized)
                {
                    _factory.Shutdown();
                }
            }
            catch
            {
                // Best-effort shutdown during process teardown.
            }
        });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stoppingRegistration.Dispose();
        return Task.CompletedTask;
    }
}
