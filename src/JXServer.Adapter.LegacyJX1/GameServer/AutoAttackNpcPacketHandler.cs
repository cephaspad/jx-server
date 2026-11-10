using System;
using System.Threading;
using System.Threading.Tasks;
using JXServer.Adapter.LegacyJX1.Logging;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.Items;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.Combat;
using JXServer.ZoneEngine.Items;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.Logging;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_autoattacknpc</c> (<c>0x87</c>, <c>26</c> bytes), routing <c>SubCommand = 0x02</c> emergency Town Portal (`FR-013`)
/// and <c>SubCommand = 0x03</c> wrapped <c>0x4B</c> (walk) and <c>0x4D</c> (skill) frames (`FR-002`).
/// </summary>
public sealed class AutoAttackNpcPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;
    private readonly ILogger<AutoAttackNpcPacketHandler> _logger;

    public byte Opcode => ProtocolOpcodes.c2s_autoattacknpc;

    public AutoAttackNpcPacketHandler(
        IZoneClusterRouter zoneClusterRouter,
        ILogger<AutoAttackNpcPacketHandler> logger)
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

        if (!AutoAttackNpcC2SPacket.TryParse(payload.Span, out AutoAttackNpcC2SPacket packet))
        {
            return ValueTask.FromResult(false);
        }

        if (packet.SubCommand == AutoAttackNpcC2SPacket.SubCommandEmergencyTownPortal)
        {
            bool tpDispatched = _zoneClusterRouter.TryDispatchToZone(
                session.AttachedMapId.Value,
                new UseInventoryItemZoneCommand(
                    session.CharacterId.Value,
                    session.LeaseEpoch,
                    RuntimeItemId: 0,
                    Place: ItemContainerPlace.None,
                    GridX: 0,
                    GridY: 0,
                    IsEmergencyTownPortal: true));
            return ValueTask.FromResult(tpDispatched);
        }

        if (packet.SubCommand == AutoAttackNpcC2SPacket.SubCommandWrappedAction)
        {
            ReadOnlySpan<byte> inner = packet.InnerPayload.Span;
            if (inner.Length >= NpcWalkC2SPacket.PayloadSize && inner[0] == ProtocolOpcodes.c2s_npcwalk)
            {
                if (!NpcWalkC2SPacket.TryParse(inner.Slice(0, NpcWalkC2SPacket.PayloadSize), out NpcWalkC2SPacket walk))
                {
                    return ValueTask.FromResult(true);
                }

                if (!MovementAntiCheatConsts.IsValidCheckValue(
                    walk.CheckValue,
                    session.ClientToken1,
                    session.ClientToken2,
                    MovementAntiCheatConsts.WalkCheckKey))
                {
                    LegacyAdapterLogMessages.LogInvalidMovementCheckValue(
                        _logger,
                        session.ConnectionId,
                        session.CharacterId.Value,
                        ProtocolOpcodes.c2s_npcwalk,
                        walk.CheckValue);
                    return ValueTask.FromResult(true);
                }

                bool walkDispatched = _zoneClusterRouter.TryDispatchToZone(
                    session.AttachedMapId.Value,
                    new MovePlayerZoneCommand(
                        session.CharacterId.Value,
                        session.LeaseEpoch,
                        walk.TargetMpsX,
                        walk.TargetMpsY,
                        IsRun: false));
                return ValueTask.FromResult(walkDispatched);
            }

            if (inner.Length >= NpcSkillC2SPacket.PayloadSize && inner[0] == ProtocolOpcodes.c2s_npcskill)
            {
                if (!NpcSkillC2SPacket.TryParse(inner.Slice(0, NpcSkillC2SPacket.PayloadSize), out NpcSkillC2SPacket skill))
                {
                    return ValueTask.FromResult(true);
                }

                if (!MovementAntiCheatConsts.IsValidCheckValue(
                    skill.CheckValue,
                    session.ClientToken1,
                    session.ClientToken2,
                    MovementAntiCheatConsts.SkillCheckKey))
                {
                    LegacyAdapterLogMessages.LogInvalidMovementCheckValue(
                        _logger,
                        session.ConnectionId,
                        session.CharacterId.Value,
                        ProtocolOpcodes.c2s_npcskill,
                        skill.CheckValue);
                    return ValueTask.FromResult(true);
                }

                bool skillDispatched = _zoneClusterRouter.TryDispatchToZone(
                    session.AttachedMapId.Value,
                    new CastSkillZoneCommand(
                        session.CharacterId.Value,
                        session.LeaseEpoch,
                        skill.SkillId,
                        skill.TargetMpsX,
                        skill.TargetMpsYOrNpcId));
                return ValueTask.FromResult(skillDispatched);
            }
        }

        return ValueTask.FromResult(true);
    }
}
