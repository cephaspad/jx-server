using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Contracts.Characters;
using JXServer.Contracts.SessionLeases;

namespace JXServer.Adapter.LegacyJX1.Bishop;

/// <summary>
/// Handles <c>c2s_newplayer</c> (<c>0x45</c>): creates a character, sends <c>s2c_rolenewdelresponse</c> (<c>0x8C</c>),
/// and on success immediately sends <c>s2c_notifyplayerlogin</c> (<c>0x34</c>) (`FR-005`).
/// </summary>
public sealed class CreateCharacterPacketHandler : IBishopPacketHandler
{
    private readonly ICharacterAppService _characterAppService;

    public byte Opcode => ProtocolOpcodes.c2s_newplayer;

    public CreateCharacterPacketHandler(ICharacterAppService characterAppService)
    {
        _characterAppService = characterAppService;
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

        if (!CreateCharacterC2SPacket.TryParse(payload.Span, out CreateCharacterC2SPacket request))
        {
            return false;
        }

        CreateCharacterResultDto result = await _characterAppService.CreateCharacterAsync(
            new CreateCharacterDto(
                session.AuthenticatedAccountId.Value,
                session.LeaseEpoch,
                request.RoleNameUnicode,
                request.Gender,
                request.Series,
                request.NativePlaceId),
            cancellationToken);

        ushort effectiveMapId = result.Character?.MapId ?? request.NativePlaceId;
        string effectiveName = result.Character?.RoleNameUnicode ?? request.RoleNameUnicode;

        RoleNewDelResponseS2CPacket newDelReply = RoleNewDelResponseS2CPacket.Create(
            effectiveName,
            result.Succeeded,
            effectiveMapId,
            request.Identity);

        await session.SendPacketAsync(newDelReply, cancellationToken);

        if (result.Succeeded && result.HandoffTicket is { } ticket)
        {
            NotifyPlayerLoginS2CPacket notifyReply = NotifyPlayerLoginS2CPacket.Create(
                ticket.TicketGuid,
                ticket.RoleNameUnicode,
                ticket.Permitted,
                ticket.GameServerHost,
                ticket.GameServerPort,
                request.Identity);

            await session.SendPacketAsync(notifyReply, cancellationToken);
        }

        return true;
    }
}
