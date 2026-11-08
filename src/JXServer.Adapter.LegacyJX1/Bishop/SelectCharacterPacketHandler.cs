using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Contracts.SessionLeases;

namespace JXServer.Adapter.LegacyJX1.Bishop;

/// <summary>
/// Handles <c>c2s_dbplayerselect</c> (<c>0x64</c>), decrypts the role name using the <c>0x38BC</c> second-layer XOR cipher,
/// and sends <c>s2c_notifyplayerlogin</c> (<c>0x34</c>) carrying the single-use 16-byte handoff <c>GUID</c> (`FR-007`).
/// </summary>
public sealed class SelectCharacterPacketHandler : IBishopPacketHandler
{
    private readonly ISessionLeaseAppService _sessionLeaseAppService;

    public byte Opcode => ProtocolOpcodes.c2s_dbplayerselect;

    public SelectCharacterPacketHandler(ISessionLeaseAppService sessionLeaseAppService)
    {
        _sessionLeaseAppService = sessionLeaseAppService;
    }

    public async ValueTask<bool> HandleAsync(
        BishopClientSession session,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        if (!session.IsAuthenticated || !session.AuthenticatedAccountId.HasValue)
        {
            return false;
        }

        if (!SelectCharacterC2SPacket.TryParse(payload.Span, out SelectCharacterC2SPacket request) ||
            !request.TryDecodeRoleName(out string roleNameUnicode))
        {
            return false;
        }

        WorldHandoffTicketDto ticket = await _sessionLeaseAppService.SelectCharacterForWorldEntryAsync(
            new SelectCharacterInput(session.AuthenticatedAccountId.Value, session.LeaseEpoch, roleNameUnicode),
            cancellationToken);

        NotifyPlayerLoginS2CPacket reply = NotifyPlayerLoginS2CPacket.Create(
            ticket.TicketGuid,
            ticket.RoleNameUnicode,
            ticket.Permitted,
            ticket.GameServerHost,
            ticket.GameServerPort,
            request.Identity);

        await session.SendPacketAsync(reply, cancellationToken);
        return true;
    }
}
