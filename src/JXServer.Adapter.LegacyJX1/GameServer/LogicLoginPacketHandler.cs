using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Contracts.Characters;
using JXServer.Contracts.SessionLeases;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Handles <c>c2s_logiclogin</c> (<c>0x42</c>): redeems the single-use 16-byte <c>GUID</c> handoff ticket
/// and dispatches <see cref="AttachPlayerZoneCommand"/> to the target <c>MapZoneWorker</c> (`FR-008`, `FR-009`).
/// </summary>
public sealed class LogicLoginPacketHandler : IGameServerPacketHandler
{
    private readonly ISessionLeaseAppService _sessionLeaseAppService;
    private readonly IZoneClusterRouter _zoneClusterRouter;

    public byte Opcode => ProtocolOpcodes.c2s_logiclogin;

    public LogicLoginPacketHandler(
        ISessionLeaseAppService sessionLeaseAppService,
        IZoneClusterRouter zoneClusterRouter)
    {
        _sessionLeaseAppService = sessionLeaseAppService;
        _zoneClusterRouter = zoneClusterRouter;
    }

    public async ValueTask<bool> HandleAsync(
        GameServerClientSession session,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        if (session.IsAttachedToWorld)
        {
            return false;
        }

        if (!LogicLoginC2SPacket.TryParse(payload.Span, out LogicLoginC2SPacket request))
        {
            return false;
        }

        Guid ticketGuid = request.GetTicketGuid();
        RedeemHandoffTicketResultDto redeemResult = await _sessionLeaseAppService.RedeemWorldHandoffTicketAsync(
            new RedeemHandoffTicketInput(ticketGuid, session.ConnectionId),
            cancellationToken);

        if (!redeemResult.Succeeded || redeemResult.Character is not { } character)
        {
            return false;
        }

        session.SetWorldAttached(character.AccountId, character.Id, character.MapId, redeemResult.LeaseEpoch);

        bool dispatched = _zoneClusterRouter.TryDispatchToZone(
            character.MapId,
            new AttachPlayerZoneCommand(character, redeemResult.LeaseEpoch, session));

        return dispatched;
    }
}
