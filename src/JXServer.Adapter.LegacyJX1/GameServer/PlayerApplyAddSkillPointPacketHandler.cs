using System.Threading;
using System.Threading.Tasks;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.Combat;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_playerapplyaddskillpoint</c> (<c>0x58</c>) and dispatches <see cref="AllocateSkillPointZoneCommand"/> (`FR-007`).
/// </summary>
public sealed class PlayerApplyAddSkillPointPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_playeraddskillpoint;

    public PlayerApplyAddSkillPointPacketHandler(IZoneClusterRouter zoneClusterRouter)
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

        if (!PlayerApplyAddSkillPointC2SPacket.TryParse(payload.Span, out PlayerApplyAddSkillPointC2SPacket request))
        {
            return ValueTask.FromResult(false);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new AllocateSkillPointZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                request.SkillId,
                request.PointsToAdd));

        return ValueTask.FromResult(dispatched);
    }
}
