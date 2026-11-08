using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles the 1-byte <c>c2sCloseConnection</c> (<c>0x9B</c>) clean logout packet (`FR-011`).
/// </summary>
public sealed class CloseConnectionPacketHandler : IGameServerPacketHandler
{
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2sCloseConnection;

    public CloseConnectionPacketHandler(IZoneClusterRouter zoneClusterRouter)
    {
        _zoneClusterRouter = zoneClusterRouter;
    }

    public async ValueTask<bool> HandleAsync(
        GameServerClientSession session,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        if (!CloseConnectionC2SPacket.TryParse(payload.Span, out _))
        {
            return false;
        }

        if (session.IsAttachedToWorld &&
            session.CharacterId.HasValue &&
            session.AttachedMapId.HasValue)
        {
            session.MarkCleanLogoutCompleted();
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (_zoneClusterRouter.TryDispatchToZone(
                    session.AttachedMapId.Value,
                    new DetachPlayerZoneCommand(
                        session.CharacterId.Value,
                        session.LeaseEpoch,
                        PlayerDetachReason.CleanLogout,
                        tcs)))
            {
                await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }
}
