using System.Threading;
using System.Threading.Tasks;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.Items;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_playerpickupitem</c> (<c>0x5A</c>) and dispatches <see cref="PickupGroundObjectZoneCommand"/> (`FR-011`, `FR-012`).
/// </summary>
public sealed class PlayerPickupItemPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_playerpickupitem;

    public PlayerPickupItemPacketHandler(IZoneClusterRouter zoneClusterRouter)
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

        if (!PlayerPickupItemC2SPacket.TryParse(payload.Span, out PlayerPickupItemC2SPacket packet))
        {
            return ValueTask.FromResult(false);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new PickupGroundObjectZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                packet.ObjId,
                packet.TargetPlace,
                packet.TargetGridX,
                packet.TargetGridY));

        return ValueTask.FromResult(dispatched);
    }
}
