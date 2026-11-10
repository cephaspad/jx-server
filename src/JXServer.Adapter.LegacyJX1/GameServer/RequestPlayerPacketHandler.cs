using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_requestplayer</c> (<c>0x48</c>) and dispatches <see cref="RequestEntitySyncZoneCommand"/> (`FR-004`).
/// </summary>
public sealed class RequestPlayerPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_requestplayer;

    public RequestPlayerPacketHandler(IZoneClusterRouter zoneClusterRouter)
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

        if (!RequestPlayerC2SPacket.TryParse(payload.Span, out RequestPlayerC2SPacket request))
        {
            return ValueTask.FromResult(false);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new RequestEntitySyncZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                request.TargetNpcId,
                IsPlayerRequest: true));

        return ValueTask.FromResult(dispatched);
    }
}
