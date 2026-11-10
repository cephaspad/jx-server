using System.Threading;
using System.Threading.Tasks;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.Combat;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_playeraddbaseattribute</c> (<c>0x57</c>) and dispatches <see cref="AllocateBaseAttributeZoneCommand"/> (`FR-007`).
/// </summary>
public sealed class PlayerAddBaseAttributePacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_playeraddbaseattribute;

    public PlayerAddBaseAttributePacketHandler(IZoneClusterRouter zoneClusterRouter)
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

        if (!PlayerAddBaseAttributeC2SPacket.TryParse(payload.Span, out PlayerAddBaseAttributeC2SPacket request))
        {
            return ValueTask.FromResult(false);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new AllocateBaseAttributeZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                request.Attribute,
                request.PointsToAdd));

        return ValueTask.FromResult(dispatched);
    }
}
