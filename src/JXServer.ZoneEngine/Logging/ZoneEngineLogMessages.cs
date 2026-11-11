using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.Logging;

namespace JXServer.ZoneEngine.Logging;

/// <summary>
/// High-performance source-generated log messages for <c>JXServer.ZoneEngine</c>.
/// </summary>
public static partial class ZoneEngineLogMessages
{
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Player '{RoleName}' (CharacterId={CharacterId}, NpcId={RuntimeNpcId}, Epoch={LeaseEpoch}) attached to Map {MapId} at ({MpsX}, {MpsY}).")]
    public static partial void LogPlayerAttached(
        ILogger logger,
        string roleName,
        Guid characterId,
        uint runtimeNpcId,
        long leaseEpoch,
        ushort mapId,
        int mpsX,
        int mpsY);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "Player '{RoleName}' (CharacterId={CharacterId}, NpcId={RuntimeNpcId}, Epoch={LeaseEpoch}) detached from Map {MapId} (Reason={Reason}).")]
    public static partial void LogPlayerDetached(
        ILogger logger,
        string roleName,
        Guid characterId,
        uint runtimeNpcId,
        long leaseEpoch,
        ushort mapId,
        PlayerDetachReason reason);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "Spawned {NpcCount} static NPCs/monsters across regions on Map {MapId} ('{MapName}').")]
    public static partial void LogStaticNpcsSpawned(
        ILogger logger,
        int npcCount,
        ushort mapId,
        string mapName);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Information,
        Message = "Player '{RoleName}' (NpcId={RuntimeNpcId}) transitioned via trap 0x{TrapId:X8} from Map {SourceMapId} to Map {TargetMapId} ({TargetMpsX}, {TargetMpsY}).")]
    public static partial void LogPlayerMapTransition(
        ILogger logger,
        string roleName,
        uint runtimeNpcId,
        uint trapId,
        ushort sourceMapId,
        ushort targetMapId,
        int targetMpsX,
        int targetMpsY);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Debug,
        Message = "Entity {AttackerNpcId} dealt {FinalDamage} damage to Entity {DefenderNpcId} via Skill {SkillId} on Map {MapId} (RemainingLife={RemainingLife}).")]
    public static partial void LogCombatDamageApplied(
        ILogger logger,
        uint attackerNpcId,
        uint defenderNpcId,
        ushort skillId,
        int finalDamage,
        int remainingLife,
        ushort mapId);

    [LoggerMessage(
        EventId = 3006,
        Level = LogLevel.Information,
        Message = "Entity {DefeatedNpcId} was defeated by Entity {KillerNpcId} on Map {MapId} (ExpAwarded={ExpAwarded}, DroppedObjects={DroppedObjects}).")]
    public static partial void LogEntityDefeated(
        ILogger logger,
        uint defeatedNpcId,
        uint killerNpcId,
        ushort mapId,
        long expAwarded,
        int droppedObjects);

    [LoggerMessage(
        EventId = 3007,
        Level = LogLevel.Information,
        Message = "Player '{RoleName}' (NpcId={RuntimeNpcId}) leveled up to Level {NewLevel} on Map {MapId}.")]
    public static partial void LogPlayerLeveledUp(
        ILogger logger,
        string roleName,
        uint runtimeNpcId,
        ushort newLevel,
        ushort mapId);

    [LoggerMessage(
        EventId = 3008,
        Level = LogLevel.Debug,
        Message = "Player '{RoleName}' (CharacterId={CharacterId}) skill cast {SkillId} rejected: {Reason}.")]
    public static partial void LogSkillCastRejected(
        ILogger logger,
        string roleName,
        Guid characterId,
        int skillId,
        string reason);

    [LoggerMessage(
        EventId = 3009,
        Level = LogLevel.Warning,
        Message = "Dropped unregistered zone command '{CommandType}' on Map {MapId} at Frame {CurrentFrame} (CharacterId={CharacterId}).")]
    public static partial void LogUnhandledZoneCommand(
        ILogger logger,
        string commandType,
        ushort mapId,
        long currentFrame,
        Guid? characterId);

    [LoggerMessage(
        EventId = 3010,
        Level = LogLevel.Error,
        Message = "Unhandled exception in handler for command '{CommandType}' on Map {MapId} at Frame {CurrentFrame} (CharacterId={CharacterId}).")]
    public static partial void LogZoneCommandFaulted(
        ILogger logger,
        Exception exception,
        string commandType,
        ushort mapId,
        long currentFrame,
        Guid? characterId);

    [LoggerMessage(
        EventId = 3011,
        Level = LogLevel.Error,
        Message = "Unhandled exception in tick system '{SystemName}' (Phase={Phase}) on Map {MapId} at Frame {CurrentFrame}.")]
    public static partial void LogZoneTickSystemFaulted(
        ILogger logger,
        Exception exception,
        string systemName,
        ZoneTickPhase phase,
        ushort mapId,
        long currentFrame);

    [LoggerMessage(
        EventId = 3012,
        Level = LogLevel.Error,
        Message = "Unhandled exception cleaning up feature slot '{SlotType}' for CharacterId={CharacterId} on Map {MapId} (Reason={Reason}).")]
    public static partial void LogPlayerFeatureSlotCleanupFaulted(
        ILogger logger,
        Exception exception,
        string slotType,
        Guid characterId,
        ushort mapId,
        PlayerDetachReason reason);
}
