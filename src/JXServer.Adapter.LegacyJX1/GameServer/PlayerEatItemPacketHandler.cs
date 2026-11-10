using System.Threading;
using System.Threading.Tasks;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.Items;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_playereatitem</c> (<c>0x59</c>) and dispatches <see cref="UseInventoryItemZoneCommand"/> (`FR-013`).
/// </summary>
public sealed class PlayerEatItemPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_playereatitem;

    public PlayerEatItemPacketHandler(IZoneClusterRouter zoneClusterRouter)
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

        if (!PlayerEatItemC2SPacket.TryParse(payload.Span, out PlayerEatItemC2SPacket packet))
        {
            return ValueTask.FromResult(false);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new UseInventoryItemZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                packet.RuntimeItemId,
                packet.ContainerPlace,
                packet.GridX,
                packet.GridY));

        return ValueTask.FromResult(dispatched);
    }
}
