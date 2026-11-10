using System;
using System.Threading;
using System.Threading.Tasks;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.Combat;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_pkapplychangenormalflag</c> (<c>0x76</c>, <c>2</c> bytes) and dispatches <see cref="ChangePkModeZoneCommand"/> (`FR-005`).
/// </summary>
public sealed class PkApplyChangeNormalFlagPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_pkapplychangenormalflag;

    public PkApplyChangeNormalFlagPacketHandler(IZoneClusterRouter zoneClusterRouter)
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

        if (!PkApplyChangeNormalFlagC2SPacket.TryParse(payload.Span, out PkApplyChangeNormalFlagC2SPacket request))
        {
            return ValueTask.FromResult(false);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new ChangePkModeZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                request.PkMode));

        return ValueTask.FromResult(dispatched);
    }
}
