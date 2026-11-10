using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.Chats;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_playertalk</c> (<c>0x52</c>) and dispatches <see cref="SendNearbyTalkZoneCommand"/> (`FR-012`).
/// </summary>
public sealed class PlayerTalkPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_playertalk;

    public PlayerTalkPacketHandler(IZoneClusterRouter zoneClusterRouter)
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

        if (!PlayerTalkC2SPacket.TryParse(payload.Span, out PlayerTalkC2SPacket request))
        {
            return ValueTask.FromResult(false);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new SendNearbyTalkZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                request.MessageUtf8));

        return ValueTask.FromResult(dispatched);
    }
}
