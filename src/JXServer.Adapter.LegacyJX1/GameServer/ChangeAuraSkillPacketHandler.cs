using System;
using System.Threading;
using System.Threading.Tasks;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.Combat;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_changeauraskill</c> (<c>0x6F</c>, <c>5</c> bytes) and dispatches <see cref="ChangeAuraSkillZoneCommand"/> (`FR-002`).
/// </summary>
public sealed class ChangeAuraSkillPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_changeauraskill;

    public ChangeAuraSkillPacketHandler(IZoneClusterRouter zoneClusterRouter)
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

        if (!ChangeAuraSkillC2SPacket.TryParse(payload.Span, out ChangeAuraSkillC2SPacket request))
        {
            return ValueTask.FromResult(false);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new ChangeAuraSkillZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                request.AuraSkillId));

        return ValueTask.FromResult(dispatched);
    }
}
