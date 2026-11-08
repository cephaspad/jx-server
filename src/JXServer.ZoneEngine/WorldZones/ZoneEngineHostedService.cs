using Microsoft.Extensions.Hosting;

namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Background service that starts and supervises all sharded 18-Hz <see cref="MapZoneWorker"/> loops.
/// </summary>
public sealed class ZoneEngineHostedService : BackgroundService
{
    private readonly ZoneClusterRouter _clusterRouter;

    public ZoneEngineHostedService(ZoneClusterRouter clusterRouter)
    {
        _clusterRouter = clusterRouter;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Task[] tasks = _clusterRouter.Workers
            .Select(w => Task.Run(() => w.RunLoopAsync(stoppingToken), stoppingToken))
            .ToArray();

        return Task.WhenAll(tasks);
    }
}
