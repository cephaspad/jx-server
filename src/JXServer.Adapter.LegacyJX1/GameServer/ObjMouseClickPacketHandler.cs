using System.Threading;
using System.Threading.Tasks;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.Items;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_objmouseclick</c> (<c>0x72</c>) and dispatches <see cref="RequestGroundObjectSyncZoneCommand"/> (`FR-011`).
/// </summary>
public sealed class ObjMouseClickPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_objmouseclick;

    public ObjMouseClickPacketHandler(IZoneClusterRouter zoneClusterRouter)
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

        if (!ObjMouseClickC2SPacket.TryParse(payload.Span, out ObjMouseClickC2SPacket request))
        {
            return ValueTask.FromResult(false);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new RequestGroundObjectSyncZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                request.ObjId));

        return ValueTask.FromResult(dispatched);
    }
}
