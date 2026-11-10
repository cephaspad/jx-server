using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_npcsit</c> (<c>0x71</c>) and dispatches <see cref="TogglePlayerSitZoneCommand"/> (`FR-008`).
/// </summary>
public sealed class NpcSitPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_npcsit;

    public NpcSitPacketHandler(IZoneClusterRouter zoneClusterRouter)
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

        if (!NpcSitC2SPacket.TryParse(payload.Span, out NpcSitC2SPacket request))
        {
            return ValueTask.FromResult(false);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new TogglePlayerSitZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                request.SitFlag != 0));

        return ValueTask.FromResult(dispatched);
    }
}
