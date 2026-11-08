using JXServer.ZoneEngine.Configuration;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JXServer.ZoneEngine;

/// <summary>
/// Dependency injection registration extensions for the 18-Hz <c>JXServer.ZoneEngine</c>.
/// </summary>
public static class ZoneEngineServiceCollectionExtensions
{
    public static IServiceCollection AddJXServerZoneEngine(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<ZoneEngineOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        OptionsBuilder<ZoneEngineOptions> optionsBuilder = services
            .AddOptions<ZoneEngineOptions>()
            .Bind(configuration.GetSection(ZoneEngineOptions.SectionName));

        if (configureOptions is not null)
        {
            optionsBuilder.Configure(configureOptions);
        }

        services.AddSingleton<ZoneClusterRouter>();
        services.AddSingleton<IZoneClusterRouter>( static sp => sp.GetRequiredService<ZoneClusterRouter>());
        services.AddHostedService<ZoneEngineHostedService>();

        return services;
    }
}
