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
}
