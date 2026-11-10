using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_npcjump</c> (<c>0x4E</c>) and dispatches <see cref="JumpPlayerZoneCommand"/> (`FR-006`).
/// </summary>
public sealed class NpcJumpPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_npcjump;

    public NpcJumpPacketHandler(IZoneClusterRouter zoneClusterRouter)
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

        if (!NpcJumpC2SPacket.TryParse(payload.Span, out NpcJumpC2SPacket request))
        {
            return ValueTask.FromResult(false);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new JumpPlayerZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                request.TargetMpsX,
                request.TargetMpsY));

        return ValueTask.FromResult(dispatched);
    }
}
