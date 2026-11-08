using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Contracts.Accounts;
using JXServer.Contracts.Characters;
using JXServer.Primitives.Accounts;

namespace JXServer.Adapter.LegacyJX1.Bishop;

/// <summary>
/// Handles <c>c2s_login</c> (<c>0x41</c>), sends <c>s2c_login</c> (<c>0x41</c>), and on success immediately sends
/// <c>s2c_roleserver_getrolelist_result</c> (<c>0x37</c>) (`FR-003`, `FR-004`).
/// </summary>
public sealed class AccountLoginPacketHandler : IBishopPacketHandler
{
    private readonly IAccountAppService _accountAppService;
    private readonly ICharacterAppService _characterAppService;

    public byte Opcode => ProtocolOpcodes.c2s_login;

    public AccountLoginPacketHandler(
        IAccountAppService accountAppService,
        ICharacterAppService characterAppService)
    {
        _accountAppService = accountAppService;
        _characterAppService = characterAppService;
    }

    public async ValueTask<bool> HandleAsync(
        BishopClientSession session,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        if (!AccountLoginC2SPacket.TryParse(payload.Span, out AccountLoginC2SPacket request))
        {
            return false;
        }

        string userName = request.GetUserName();
        string passwordHash = request.GetPasswordHash();
        bool kickOnline = request.KickOnline != 0;

        AuthenticateAccountResultDto authResult = await _accountAppService.AuthenticateAsync(
            new AuthenticateAccountInput(
                userName,
                passwordHash,
                request.ProtocolVersion,
                kickOnline,
                session.ConnectionId),
            cancellationToken);

        AccountLoginResultS2CPacket loginReply = AccountLoginResultS2CPacket.Create(
            in request,
            authResult.ResultCode,
            authResult.RemainingTimeSeconds);

        await session.SendPacketAsync(loginReply, cancellationToken);

        if (authResult.ResultCode != LoginResultCode.Success || !authResult.AccountId.HasValue)
        {
            return true;
        }

        session.SetAuthenticated(authResult.AccountId.Value, userName, authResult.LeaseEpoch);

        IReadOnlyList<CharacterSummaryDto> roster = await _characterAppService.GetCharacterRosterAsync(
            new GetCharacterRosterInput(authResult.AccountId.Value),
            cancellationToken);

        var roleListPacket = new RoleListResultS2CPacket(0, roster);
        await session.SendRawPayloadAsync(roleListPacket.ToPayloadBytes(), cancellationToken);
        return true;
    }
}
