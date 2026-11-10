using System;
using System.Threading;
using System.Threading.Tasks;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.Items;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_PlayerDivideItem</c> (<c>0x85</c>) and dispatches <see cref="DivideInventoryItemZoneCommand"/> (`FR-012`).
/// </summary>
public sealed class PlayerDivideItemPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_PlayerDivideItem;

    public PlayerDivideItemPacketHandler(IZoneClusterRouter zoneClusterRouter)
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

        if (!PlayerDivideItemC2SPacket.TryParse(payload.Span, out PlayerDivideItemC2SPacket packet) ||
            packet.DivideCount <= 0)
        {
            return ValueTask.FromResult(false);
        }

        ushort clampedDivide = (ushort)Math.Clamp(packet.DivideCount, 1, ushort.MaxValue);
        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new DivideInventoryItemZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                packet.RuntimeItemId,
                clampedDivide));

        return ValueTask.FromResult(dispatched);
    }
}
