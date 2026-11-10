using System.Threading;
using System.Threading.Tasks;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.Items;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_ThrowAwayAllMedicine</c> (<c>0x83</c>) and dispatches <see cref="DropInventoryItemZoneCommand"/> (`FR-012`).
/// </summary>
public sealed class ThrowAwayAllMedicinePacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_ThrowAwayAllMedicine;

    public ThrowAwayAllMedicinePacketHandler(IZoneClusterRouter zoneClusterRouter)
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

        if (!ThrowAwayAllMedicineC2SPacket.TryParse(payload.Span, out _))
        {
            return ValueTask.FromResult(false);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new DropInventoryItemZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                ThrowAllMedicine: true));

        return ValueTask.FromResult(dispatched);
    }
}
