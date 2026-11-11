using JXServer.Contracts.Characters;
using JXServer.GameAssets.Items;
using JXServer.GameAssets.LootTables;
using JXServer.GameAssets.NpcTemplates;
using JXServer.GameAssets.PlayerTemplates;
using JXServer.GameAssets.Skills;
using JXServer.GameAssets.WorldMaps;
using JXServer.ZoneEngine.Configuration;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        services.TryAddSingleton<ZoneAssetServices>(static sp => new ZoneAssetServices
        {
            MapCatalog = sp.GetService<IMapCatalog>(),
            NpcTemplateCatalog = sp.GetService<INpcTemplateCatalog>(),
            TrapScriptCatalog = sp.GetService<ITrapScriptCatalog>(),
            CharacterAppService = sp.GetService<ICharacterAppService>(),
            ClusterRouter = sp.GetService<IZoneClusterRouter>(),
            SkillTemplateCatalog = sp.GetService<ISkillTemplateCatalog>(),
            MissileTemplateCatalog = sp.GetService<IMissileTemplateCatalog>(),
            PlayerTemplateCatalog = sp.GetService<IPlayerTemplateCatalog>(),
            ItemTemplateCatalog = sp.GetService<IItemTemplateCatalog>(),
            LootTableCatalog = sp.GetService<ILootTableCatalog>(),
            ItemGenerator = sp.GetService<ItemGenerator>(),
        });

        services.AddZoneModule<CoreZoneModule>();
        services.AddZoneModule<Chats.ChatZoneModule>();

        services.TryAddSingleton<ZoneExtensionRegistry>(static sp =>
            ZoneExtensionRegistry.Create(sp.GetServices<IZoneModule>()));

        services.AddSingleton<ZoneClusterRouter>();
        services.AddSingleton<IZoneClusterRouter>(static sp => sp.GetRequiredService<ZoneClusterRouter>());
        services.AddHostedService<ZoneEngineHostedService>();

        return services;
    }

    public static IServiceCollection AddZoneModule<TModule>(this IServiceCollection services)
        where TModule : class, IZoneModule
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IZoneModule, TModule>());
        return services;
    }
}
