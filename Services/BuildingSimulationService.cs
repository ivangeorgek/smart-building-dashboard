using System.Diagnostics;

namespace SmartBuildingDashboard.Services;

/// <summary>A small circuit-owned timer, started only after the interactive layout renders.</summary>
public sealed class BuildingSimulationService(BuildingStateService state, ILogger<BuildingSimulationService> logger) : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _loop;

    public void Start(Func<Action, Task> dispatch)
    {
        _loop ??= RunAsync(dispatch, _cancellation.Token);
    }

    private async Task RunAsync(Func<Action, Task> dispatch, CancellationToken cancellation)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(4));
        var clock = Stopwatch.StartNew();
        try
        {
            while (await timer.WaitForNextTickAsync(cancellation))
            {
                var elapsed = clock.Elapsed;
                clock.Restart();
                // Serialize telemetry and user interactions on Blazor's renderer, avoiding shared-list races.
                await dispatch(() => state.Advance(elapsed));
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(exception, "The building simulation stopped unexpectedly.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cancellation.CancelAsync();
        if (_loop is not null) await _loop;
        _cancellation.Dispose();
    }
}
