using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles the 9-byte <c>c2s_ping</c> (<c>0x70</c>) heartbeat reply and dispatches <see cref="ProcessPlayerPingZoneCommand"/> (`FR-010`).
/// </summary>
public sealed class PingReplyPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_ping;

    public PingReplyPacketHandler(IZoneClusterRouter zoneClusterRouter)
    {
        _zoneClusterRouter = zoneClusterRouter;
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

        if (!PingReplyC2SPacket.TryParse(payload.Span, out PingReplyC2SPacket request))
        {
            return ValueTask.FromResult(false);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new ProcessPlayerPingZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                request.EchoedServerTimestamp,
                request.ClientTimestamp));

        return ValueTask.FromResult(dispatched);
    }
}
