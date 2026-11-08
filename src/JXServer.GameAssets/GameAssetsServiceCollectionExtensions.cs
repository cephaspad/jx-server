using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.PlayerTemplates;
using JXServer.GameAssets.WorldMaps;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JXServer.GameAssets;

/// <summary>
/// Dependency injection registration and startup validation extensions for <c>JXServer.GameAssets</c> (`FR-012`).
/// </summary>
public static class GameAssetsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IMapCatalog"/> and <see cref="IPlayerTemplateCatalog"/> singletons and validates critical assets on startup.
    /// </summary>
    public static IServiceCollection AddJXServerGameAssets(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<GameAssetsOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        OptionsBuilder<GameAssetsOptions> optionsBuilder = services
            .AddOptions<GameAssetsOptions>()
            .Bind(configuration.GetSection(GameAssetsOptions.SectionName));

        if (configureOptions is not null)
        {
            optionsBuilder.Configure(configureOptions);
        }

        services.AddSingleton<IMapCatalog, MapCatalog>();
        services.AddSingleton<IPlayerTemplateCatalog, PlayerTemplateCatalog>();

        return services;
    }
}
