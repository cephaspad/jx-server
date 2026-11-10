using System.Threading;
using System.Threading.Tasks;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.Items;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_playermoveitem</c> (<c>0x5B</c>) and dispatches <see cref="MoveInventoryItemZoneCommand"/> (`FR-012`).
/// </summary>
public sealed class PlayerMoveItemPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_playermoveitem;

    public PlayerMoveItemPacketHandler(IZoneClusterRouter zoneClusterRouter)
    {
        _zoneClusterRouter = zoneClusterRouter;
    }

    public ValueTask<bool> HandleAsync(
        GameServerClientSession session,
        System.ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        if (!session.IsAttachedToWorld ||
            !session.CharacterId.HasValue ||
            !session.AttachedMapId.HasValue)
        {
            return ValueTask.FromResult(false);
        }

        if (!PlayerMoveItemC2SPacket.TryParse(payload.Span, out PlayerMoveItemC2SPacket packet))
        {
            return ValueTask.FromResult(false);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new MoveInventoryItemZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                packet.DownPos,
                packet.DownX,
                packet.DownY,
                packet.UpPos,
                packet.UpX,
                packet.UpY));

        return ValueTask.FromResult(dispatched);
    }
}
