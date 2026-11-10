using JXServer.Adapter.LegacyJX1.Logging;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.Logging;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_npcrun</c> (<c>0x4C</c>), verifies the <c>CheckValue</c> against key <c>0x1D78</c>, and dispatches <see cref="MovePlayerZoneCommand"/> (`FR-005`, `FR-006`).
/// </summary>
public sealed class NpcRunPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;
    private readonly ILogger<NpcRunPacketHandler> _logger;

    public byte Opcode => ProtocolOpcodes.c2s_npcrun;

    public NpcRunPacketHandler(
        IZoneClusterRouter zoneClusterRouter,
        ILogger<NpcRunPacketHandler> logger)
    {
        _zoneClusterRouter = zoneClusterRouter;
        _logger = logger;
    }

    public ValueTask<bool> HandleAsync(
        GameServerClientSession session,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        if (!session.IsAttachedToWorld ||
            !session.CharacterId.HasValue ||
            !session.AttachedMapId.HasValue)
        {
            return ValueTask.FromResult(false);
        }

        if (!NpcRunC2SPacket.TryParse(payload.Span, out NpcRunC2SPacket request))
        {
            return ValueTask.FromResult(false);
        }

        if (!MovementAntiCheatConsts.IsValidCheckValue(
            request.CheckValue,
            session.ClientToken1,
            session.ClientToken2,
            MovementAntiCheatConsts.RunCheckKey))
        {
            LegacyAdapterLogMessages.LogInvalidMovementCheckValue(
                _logger,
                session.ConnectionId,
                session.CharacterId.Value,
                Opcode,
                request.CheckValue);
            return ValueTask.FromResult(true);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new MovePlayerZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                request.TargetMpsX,
                request.TargetMpsY,
                IsRun: true));

        return ValueTask.FromResult(dispatched);
    }
}
