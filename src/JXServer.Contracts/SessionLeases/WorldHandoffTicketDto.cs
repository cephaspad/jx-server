namespace JXServer.Contracts.SessionLeases;

/// <summary>
/// DTO representing a single-use 16-byte GameServer world handoff ticket (<c>0x34</c> <c>s2c_notifyplayerlogin</c>).
/// </summary>
public sealed record WorldHandoffTicketDto(
    bool Permitted,
    Guid TicketGuid,
    long LeaseEpoch,
    Guid AccountId,
    Guid CharacterId,
    string RoleNameUnicode,
    ushort TargetMapId,
    string GameServerHost,
    ushort GameServerPort);
