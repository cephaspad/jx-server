using Microsoft.Extensions.Logging;

namespace JXServer.Adapter.LegacyJX1.Logging;

/// <summary>
/// High-performance source-generated log messages for <c>JXServer.Adapter.LegacyJX1</c>.
/// </summary>
public static partial class LegacyAdapterLogMessages
{
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Bishop TCP listener started on {Endpoint}.")]
    public static partial void LogBishopListenerStarted(ILogger logger, string endpoint);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Information,
        Message = "GameServer TCP listener started on {Endpoint}.")]
    public static partial void LogGameServerListenerStarted(ILogger logger, string endpoint);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Warning,
        Message = "Rejected malformed or unauthorized C2S packet on session '{ConnectionId}' (Opcode=0x{Opcode:X2}, Length={Length}).")]
    public static partial void LogMalformedOrUnauthorizedPacket(
        ILogger logger,
        string connectionId,
        byte opcode,
        int length);

    [LoggerMessage(
        EventId = 4004,
        Level = LogLevel.Warning,
        Message = "Rejected movement command on session '{ConnectionId}' (CharacterId={CharacterId}, Opcode=0x{Opcode:X2}) due to invalid dwCheckValue=0x{CheckValue:X8}.")]
    public static partial void LogInvalidMovementCheckValue(
        ILogger logger,
        string connectionId,
        Guid characterId,
        byte opcode,
        uint checkValue);

    [LoggerMessage(
        EventId = 4005,
        Level = LogLevel.Warning,
        Message = "Rejected combat or inventory packet on session '{ConnectionId}' (CharacterId={CharacterId}, Opcode=0x{Opcode:X2}, Reason={Reason}).")]
    public static partial void LogCombatOrInventoryPacketRejected(
        ILogger logger,
        string connectionId,
        Guid characterId,
        byte opcode,
        string reason);
}
