using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles the 9-byte <c>c2s_syncend</c> (<c>0x43</c>) reply and dispatches <see cref="CompletePlayerSyncZoneCommand"/> (`FR-009`).
/// </summary>
public sealed class SyncEndPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_syncend;

    public SyncEndPacketHandler(IZoneClusterRouter zoneClusterRouter)
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

        if (!SyncEndC2SPacket.TryParse(payload.Span, out SyncEndC2SPacket request))
        {
            return ValueTask.FromResult(false);
        }

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            session.AttachedMapId.Value,
            new CompletePlayerSyncZoneCommand(
                session.CharacterId.Value,
                session.LeaseEpoch,
                request.ClientToken,
                request.ClientTick));

        return ValueTask.FromResult(dispatched);
    }
}
