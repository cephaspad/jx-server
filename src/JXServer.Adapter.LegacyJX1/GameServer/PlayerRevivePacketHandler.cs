using System.Threading;
using System.Threading.Tasks;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.Combat;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_playerrevive</c> (<c>0x74</c>) and dispatches <see cref="RevivePlayerZoneCommand"/> (`FR-008`).
/// </summary>
public sealed class PlayerRevivePacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_npcrevive;

    public PlayerRevivePacketHandler(IZoneClusterRouter zoneClusterRouter)
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

        if (!PlayerReviveC2SPacket.TryParse(payload.Span, out PlayerReviveC2SPacket request))
        {
            return ValueTask.FromResult(false);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new RevivePlayerZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                request.ReviveType));

        return ValueTask.FromResult(dispatched);
    }
}
