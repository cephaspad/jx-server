using JXServer.Adapter.LegacyJX1;
using JXServer.Adapter.LegacyJX1.Bishop;
using JXServer.Adapter.LegacyJX1.GameServer;
using JXServer.Application;
using JXServer.Application.Configuration;
using JXServer.GameAssets;
using JXServer.GameAssets.Configuration;
using JXServer.Infrastructure;
using JXServer.ZoneEngine;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace JXServer.Integration.Tests;

/// <summary>
/// Shared in-process integration test harness hosting Bishop (<c>5622</c> dynamic port) and GameServer (<c>6666</c> dynamic port)
/// backed by an isolated in-memory SQLite database and real <c>jx-linux-bin</c> assets.
/// </summary>
public sealed class IntegrationTestServerHarness : IAsyncDisposable
{
    private readonly SqliteConnection _keepAliveSqliteConnection;
    private readonly IHost _host;

    public int BishopPort { get; private set; }

    public int GameServerPort { get; private set; }

    public IServiceProvider Services => _host.Services;

    private IntegrationTestServerHarness(SqliteConnection keepAliveSqliteConnection, IHost host)
    {
        _keepAliveSqliteConnection = keepAliveSqliteConnection;
        _host = host;
    }

    public static async Task<IntegrationTestServerHarness> StartAsync(
        bool autoProvisionAccounts = true,
        bool fallbackUnloadedStarterVillage = true,
        ushort[]? excludedWorldSetMapIds = null)
    {
        string dbName = $"file:jxserver_test_{Guid.NewGuid():N}?mode=memory&cache=shared";
        var keepAliveConn = new SqliteConnection($"Data Source={dbName}");
        await keepAliveConn.OpenAsync();

        var configDict = new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "Sqlite",
            ["Persistence:ConnectionString"] = $"Data Source={dbName}",
            ["Persistence:AutoInitializeDatabase"] = "true",
            ["GameAssets:ServerRootPath"] = @"d:\Projects\jx-linux-bin\server1",
            ["GameAssets:GatewayRootPath"] = @"d:\Projects\jx-linux-bin\gateway",
            ["GameAssets:DefaultFallbackStarterVillageMapId"] = "53",
            ["GameAssets:FallbackUnloadedStarterVillage"] = fallbackUnloadedStarterVillage.ToString(),
            ["Application:AutoProvisionAccounts"] = autoProvisionAccounts.ToString(),
            ["Application:AdvertisedGameServerHost"] = "127.0.0.1",
            ["Application:AdvertisedGameServerPort"] = "0",
            ["ZoneEngine:TicksPerSecond"] = "18",
            ["ZoneEngine:HeartbeatIntervalSeconds"] = "5",
            ["ZoneEngine:HeartbeatTimeoutSeconds"] = "15",
            ["LegacyGateway:BindAddress"] = "127.0.0.1",
            ["LegacyGateway:BishopPort"] = "0",
            ["LegacyGateway:GameServerPort"] = "0"
        };

        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(configDict);

        builder.Services
            .AddJXServerInfrastructure(builder.Configuration)
            .AddJXServerGameAssets(builder.Configuration, opts =>
            {
                if (excludedWorldSetMapIds is { Length: > 0 })
                {
                    opts.ExcludedWorldSetMapIds = excludedWorldSetMapIds;
                }
            })
            .AddJXServerApplication(builder.Configuration)
            .AddJXServerZoneEngine(builder.Configuration)
            .AddJXServerLegacyJX1Adapter(builder.Configuration);

        IHost host = builder.Build();
        await host.StartAsync();

        var bishopListener = host.Services.GetRequiredService<BishopTcpListenerHostedService>();
        var gsListener = host.Services.GetRequiredService<GameServerTcpListenerHostedService>();

        ApplicationOptions appOptions = host.Services.GetRequiredService<IOptions<ApplicationOptions>>().Value;
        appOptions.AdvertisedGameServerPort = (ushort)gsListener.BoundPort;

        return new IntegrationTestServerHarness(keepAliveConn, host)
        {
            BishopPort = bishopListener.BoundPort,
            GameServerPort = gsListener.BoundPort
        };
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
        }
        catch
        {
        }

        _host.Dispose();
        await _keepAliveSqliteConnection.DisposeAsync();
    }
}
