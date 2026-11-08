using JXServer.ZoneEngine.PlayerSessions;
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
}
