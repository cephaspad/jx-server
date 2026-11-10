using Microsoft.Extensions.Logging;

namespace JXServer.GameAssets.Logging;

/// <summary>
/// High-performance source-generated log messages for <c>JXServer.GameAssets</c>.
/// </summary>
public static partial class GameAssetsLogMessages
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Loaded {MapCount} active world maps from '{WorldSetPath}' (Eager obstacle grids: {ObstacleMapCount}).")]
    public static partial void LogWorldMapsLoaded(
        ILogger logger,
        int mapCount,
        string worldSetPath,
        int obstacleMapCount);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Loaded {TemplateCount} starter player templates, {VillageCount} starter villages, and {FilterCount} prohibited name patterns.")]
    public static partial void LogPlayerTemplatesLoaded(
        ILogger logger,
        int templateCount,
        int villageCount,
        int filterCount);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Loaded {NpcTemplateCount} NPC templates from '{NpcSettingsPath}' and {AutoExecSpawnCount} autoexec dialog NPCs.")]
    public static partial void LogNpcTemplatesLoaded(
        ILogger logger,
        int npcTemplateCount,
        string npcSettingsPath,
        int autoExecSpawnCount);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Indexed {TrapBindingCount} map trap script bindings from '{ScriptRootPath}'.")]
    public static partial void LogTrapScriptsLoaded(
        ILogger logger,
        int trapBindingCount,
        string scriptRootPath);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "Loaded {SkillCount} skill templates and {MissileCount} missile templates via '{SourceTier}'.")]
    public static partial void LogSkillAndMissileCatalogsLoaded(
        ILogger logger,
        int skillCount,
        int missileCount,
        string sourceTier);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Information,
        Message = "Loaded {ItemTemplateCount} item templates and {AffixCount} magic affixes via '{SourceTier}'.")]
    public static partial void LogItemCatalogLoaded(
        ILogger logger,
        int itemTemplateCount,
        int affixCount,
        string sourceTier);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Information,
        Message = "Loaded {LootTableCount} loot drop tables via '{SourceTier}'.")]
    public static partial void LogLootTablesLoaded(
        ILogger logger,
        int lootTableCount,
        string sourceTier);
}
