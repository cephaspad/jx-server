using JXServer.Adapter.LegacyJX1.Bishop;
using JXServer.Adapter.LegacyJX1.Configuration;
using JXServer.Adapter.LegacyJX1.GameServer;
using JXServer.Domain.SessionLeases;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JXServer.Adapter.LegacyJX1;

/// <summary>
/// Dependency injection registration extensions for the Legacy JX1 Bishop (<c>5622</c>) and GameServer (<c>6666</c>) protocol adapter.
/// </summary>
public static class LegacyJX1AdapterServiceCollectionExtensions
{
    public static IServiceCollection AddJXServerLegacyJX1Adapter(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<LegacyGatewayOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        OptionsBuilder<LegacyGatewayOptions> optionsBuilder = services
            .AddOptions<LegacyGatewayOptions>()
            .Bind(configuration.GetSection(LegacyGatewayOptions.SectionName));

        if (configureOptions is not null)
        {
            optionsBuilder.Configure(configureOptions);
        }

        services.AddSingleton<LegacySessionEvictionNotifier>();
        services.AddSingleton<ISessionEvictionNotifier>(static sp => sp.GetRequiredService<LegacySessionEvictionNotifier>());

        // Bishop packet handlers
        services.AddSingleton<IBishopPacketHandler, AccountLoginPacketHandler>();
        services.AddSingleton<IBishopPacketHandler, SelectCharacterPacketHandler>();
        services.AddSingleton<IBishopPacketHandler, CreateCharacterPacketHandler>();
        services.AddSingleton<IBishopPacketHandler, DeleteCharacterPacketHandler>();

        // GameServer packet handlers
        services.AddSingleton<IGameServerPacketHandler, LogicLoginPacketHandler>();
        services.AddSingleton<IGameServerPacketHandler, SyncEndPacketHandler>();
        services.AddSingleton<IGameServerPacketHandler, PingReplyPacketHandler>();
        services.AddSingleton<IGameServerPacketHandler, CloseConnectionPacketHandler>();

        // Listeners
        services.AddSingleton<BishopTcpListenerHostedService>();
        services.AddHostedService(static sp => sp.GetRequiredService<BishopTcpListenerHostedService>());

        services.AddSingleton<GameServerTcpListenerHostedService>();
        services.AddHostedService(static sp => sp.GetRequiredService<GameServerTcpListenerHostedService>());

        return services;
    }
}
