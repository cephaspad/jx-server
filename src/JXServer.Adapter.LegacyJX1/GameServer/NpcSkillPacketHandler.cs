using System;
using System.Threading;
using System.Threading.Tasks;
using JXServer.Adapter.LegacyJX1.Logging;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.Combat;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.Logging;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_npcskill</c> (<c>0x4D</c>), verifies <c>CheckValue</c> against key <c>0x3AB9</c>, and dispatches <see cref="CastSkillZoneCommand"/> (`FR-002`).
/// </summary>
public sealed class NpcSkillPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;
    private readonly ILogger<NpcSkillPacketHandler> _logger;

    public byte Opcode => ProtocolOpcodes.c2s_npcskill;

    public NpcSkillPacketHandler(
        IZoneClusterRouter zoneClusterRouter,
        ILogger<NpcSkillPacketHandler> logger)
    {
        _zoneClusterRouter = zoneClusterRouter;
        _logger = logger;
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

        if (!NpcSkillC2SPacket.TryParse(payload.Span, out NpcSkillC2SPacket request))
        {
            return ValueTask.FromResult(false);
        }

        if (!MovementAntiCheatConsts.IsValidCheckValue(
            request.CheckValue,
            session.ClientToken1,
            session.ClientToken2,
            MovementAntiCheatConsts.SkillCheckKey))
        {
            LegacyAdapterLogMessages.LogInvalidMovementCheckValue(
                _logger,
                session.ConnectionId,
                session.CharacterId.Value,
                Opcode,
                request.CheckValue);
            return ValueTask.FromResult(true);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new CastSkillZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                request.SkillId,
                request.TargetMpsX,
                request.TargetMpsYOrNpcId));

        return ValueTask.FromResult(dispatched);
    }
}
