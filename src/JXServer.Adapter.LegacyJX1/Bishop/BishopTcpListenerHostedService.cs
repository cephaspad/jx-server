using System.Collections.Frozen;
using System.Net;
using System.Net.Sockets;
using JXServer.Adapter.LegacyJX1.Configuration;
using JXServer.Adapter.LegacyJX1.Logging;
using JXServer.Contracts.SessionLeases;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JXServer.Adapter.LegacyJX1.Bishop;

/// <summary>
/// Hosted TCP listener for the Bishop Gateway (<c>5622</c>).
/// </summary>
public sealed class BishopTcpListenerHostedService : BackgroundService
{
    private readonly FrozenDictionary<byte, IBishopPacketHandler> _handlers;
    private readonly ISessionLeaseAppService _sessionLeaseAppService;
    private readonly LegacySessionEvictionNotifier _evictionNotifier;
    private readonly IOptions<LegacyGatewayOptions> _options;
    private readonly ILogger<BishopTcpListenerHostedService> _logger;

    private TcpListener? _listener;

    public int BoundPort { get; private set; }

    public BishopTcpListenerHostedService(
        IEnumerable<IBishopPacketHandler> handlers,
        ISessionLeaseAppService sessionLeaseAppService,
        LegacySessionEvictionNotifier evictionNotifier,
        IOptions<LegacyGatewayOptions> options,
        ILogger<BishopTcpListenerHostedService> logger)
    {
        _handlers = handlers.ToFrozenDictionary(h => h.Opcode);
        _sessionLeaseAppService = sessionLeaseAppService;
        _evictionNotifier = evictionNotifier;
        _options = options;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        LegacyGatewayOptions cfg = _options.Value;
        IPAddress ip = IPAddress.TryParse(cfg.BindAddress, out IPAddress? parsed) ? parsed : IPAddress.Any;
        _listener = new TcpListener(ip, cfg.BishopPort);
        _listener.Start();
        BoundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;

        LegacyAdapterLogMessages.LogBishopListenerStarted(_logger, _listener.LocalEndpoint.ToString() ?? $"0.0.0.0:{BoundPort}");
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
                var session = new BishopClientSession(client, _handlers, _sessionLeaseAppService, _logger);
                _evictionNotifier.RegisterBishopSession(session);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await session.RunAsync(stoppingToken);
                    }
                    finally
                    {
                        _evictionNotifier.UnregisterBishopSession(session.ConnectionId);
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
