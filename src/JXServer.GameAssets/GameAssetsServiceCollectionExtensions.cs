using System;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.Items;
using JXServer.GameAssets.LootTables;
using JXServer.GameAssets.NpcTemplates;
using JXServer.GameAssets.PlayerTemplates;
using JXServer.GameAssets.Shops;
using JXServer.GameAssets.Skills;
using JXServer.GameAssets.WorldMaps;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JXServer.GameAssets;

/// <summary>
/// Dependency injection registration and startup validation extensions for <c>JXServer.GameAssets</c> (`FR-001`, `FR-004`, `FR-008`, `FR-012`).
/// </summary>
public static class GameAssetsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="INpcTemplateCatalog"/>, <see cref="ITrapScriptCatalog"/>, <see cref="IMapCatalog"/>,
    /// <see cref="IPlayerTemplateCatalog"/>, <see cref="ISkillTemplateCatalog"/>, <see cref="IMissileTemplateCatalog"/>,
    /// <see cref="IItemTemplateCatalog"/>, and <see cref="ILootTableCatalog"/> singletons.
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

        services.AddSingleton<ISkillTemplateCatalog, SkillTemplateCatalog>();
        services.AddSingleton<IMissileTemplateCatalog, MissileTemplateCatalog>();
        services.AddSingleton<IItemTemplateCatalog, ItemTemplateCatalog>();
        services.AddSingleton<ItemGenerator>();
        services.AddSingleton<ILootTableCatalog, LootTableCatalog>();
        services.AddSingleton<INpcTemplateCatalog, NpcTemplateCatalog>();
        services.AddSingleton<ITrapScriptCatalog, TrapScriptCatalog>();
        services.AddSingleton<IMapCatalog, MapCatalog>();
        services.AddSingleton<IPlayerTemplateCatalog, PlayerTemplateCatalog>();
        services.AddSingleton<IShopCatalog, ShopCatalog>();

        return services;
    }
}
