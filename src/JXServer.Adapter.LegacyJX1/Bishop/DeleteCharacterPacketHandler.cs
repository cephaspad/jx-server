using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Contracts.Characters;

namespace JXServer.Adapter.LegacyJX1.Bishop;

/// <summary>
/// Handles <c>c2s_roleserver_deleteplayer</c> (<c>0x3A</c>) and sends <c>s2c_rolenewdelresponse</c> (<c>0x8C</c>) (`FR-006`).
/// </summary>
public sealed class DeleteCharacterPacketHandler : IBishopPacketHandler
{
    private readonly ICharacterAppService _characterAppService;

    public byte Opcode => ProtocolOpcodes.c2s_roleserver_deleteplayer;

    public DeleteCharacterPacketHandler(ICharacterAppService characterAppService)
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

        if (!DeleteCharacterC2SPacket.TryParse(payload.Span, out DeleteCharacterC2SPacket request))
        {
            return false;
        }

        string roleName = request.GetRoleName();
        string passwordHash = request.GetPasswordHash();

        DeleteCharacterResultDto result = await _characterAppService.DeleteCharacterAsync(
            new DeleteCharacterDto(
                session.AuthenticatedAccountId.Value,
                session.LeaseEpoch,
                roleName,
                passwordHash),
            cancellationToken);

        RoleNewDelResponseS2CPacket reply = RoleNewDelResponseS2CPacket.Create(
            result.RoleNameUnicode,
            result.Succeeded,
            nativePlaceId: 0,
            identity: request.Identity);

        await session.SendPacketAsync(reply, cancellationToken);
        return true;
    }
}
