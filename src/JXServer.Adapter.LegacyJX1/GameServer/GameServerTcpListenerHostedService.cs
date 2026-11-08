using System.Collections.Frozen;
using System.Net;
using System.Net.Sockets;
using JXServer.Adapter.LegacyJX1.Bishop;
using JXServer.Adapter.LegacyJX1.Configuration;
using JXServer.Adapter.LegacyJX1.Logging;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Hosted TCP listener for the GameServer (<c>6666</c>).
/// </summary>
public sealed class GameServerTcpListenerHostedService : BackgroundService
{
    private readonly FrozenDictionary<byte, IGameServerPacketHandler> _handlers;
    private readonly IZoneClusterRouter _zoneClusterRouter;
    private readonly LegacySessionEvictionNotifier _evictionNotifier;
    private readonly IOptions<LegacyGatewayOptions> _options;
    private readonly ILogger<GameServerTcpListenerHostedService> _logger;

    private TcpListener? _listener;

    public int BoundPort { get; private set; }

    public GameServerTcpListenerHostedService(
        IEnumerable<IGameServerPacketHandler> handlers,
        IZoneClusterRouter zoneClusterRouter,
        LegacySessionEvictionNotifier evictionNotifier,
        IOptions<LegacyGatewayOptions> options,
        ILogger<GameServerTcpListenerHostedService> logger)
    {
        _handlers = handlers.ToFrozenDictionary(h => h.Opcode);
        _zoneClusterRouter = zoneClusterRouter;
        _evictionNotifier = evictionNotifier;
        _options = options;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        LegacyGatewayOptions cfg = _options.Value;
        IPAddress ip = IPAddress.TryParse(cfg.BindAddress, out IPAddress? parsed) ? parsed : IPAddress.Any;
        _listener = new TcpListener(ip, cfg.GameServerPort);
        _listener.Start();
        BoundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;

        LegacyAdapterLogMessages.LogGameServerListenerStarted(_logger, _listener.LocalEndpoint.ToString() ?? $"0.0.0.0:{BoundPort}");
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_listener is null)
        {
            return;
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(stoppingToken);
                var session = new GameServerClientSession(client, _handlers, _zoneClusterRouter, _logger);
                _evictionNotifier.RegisterGameServerSession(session);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await session.RunAsync(stoppingToken);
                    }
                    finally
                    {
                        _evictionNotifier.UnregisterGameServerSession(session.ConnectionId);
                    }
                }, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (SocketException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _listener?.Stop();
        }
        catch
        {
        }

        return base.StopAsync(cancellationToken);
    }
}
