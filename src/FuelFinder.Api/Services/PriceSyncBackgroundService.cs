namespace FuelFinder.Api.Services;

/// <summary>
/// Runs PriceSyncService on startup and then every 30 minutes.
/// Uses IServiceScopeFactory so the scoped PriceSyncService gets a fresh scope each run.
/// </summary>
public class PriceSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<PriceSyncBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunSyncAsync(stoppingToken);

        using var timer = new PeriodicTimer(SyncInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSyncAsync(stoppingToken);
        }
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IPriceSyncService>();
            await svc.SyncAsync(ct);
            logger.LogInformation("Price sync complete.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Price sync background service encountered an error.");
        }
    }
}
