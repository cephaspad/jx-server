using JXServer.Primitives.SessionLeases;

namespace JXServer.Domain.SessionLeases;

/// <summary>
/// Aggregate root representing an authoritative single-active-session lease for an account (`FR-008`, `FR-015`).
/// </summary>
public sealed class SessionLease
{
    public Guid AccountId { get; private set; }

    public string UserName { get; private set; } = string.Empty;

    public long LeaseEpoch { get; private set; }

    public SessionLeaseState State { get; private set; }

    public string? BishopConnectionId { get; private set; }

    public Guid? ActiveCharacterId { get; private set; }

    public string? ActiveRoleName { get; private set; }

    public Guid? HandoffTicketGuid { get; private set; }

    public ushort? AssignedMapId { get; private set; }

    public string? GameServerConnectionId { get; private set; }

    public DateTimeOffset IssuedAtUtc { get; private set; }

    public DateTimeOffset? TicketExpiresAtUtc { get; private set; }

    public DateTimeOffset LastHeartbeatAtUtc { get; private set; }

    private SessionLease()
    {
    }

    public SessionLease(
        Guid accountId,
        string userName,
        long leaseEpoch,
        DateTimeOffset nowUtc,
        string? bishopConnectionId = null)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("AccountId cannot be empty.", nameof(accountId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        AccountId = accountId;
        UserName = userName;
        LeaseEpoch = leaseEpoch;
        State = SessionLeaseState.GatewayAuthenticated;
        BishopConnectionId = bishopConnectionId;
        IssuedAtUtc = nowUtc;
        LastHeartbeatAtUtc = nowUtc;
    }

    public bool IsActive(DateTimeOffset nowUtc, TimeSpan heartbeatTimeout)
    {
        if (State == SessionLeaseState.Offline)
        {
            return false;
        }

        if (State == SessionLeaseState.HandoffIssued &&
            TicketExpiresAtUtc.HasValue &&
            TicketExpiresAtUtc.Value <= nowUtc)
        {
            return false;
        }

        return (nowUtc - LastHeartbeatAtUtc) <= heartbeatTimeout;
    }

    public void BeginGatewaySession(long newEpoch, DateTimeOffset nowUtc, string? bishopConnectionId = null)
    {
        LeaseEpoch = newEpoch;
        State = SessionLeaseState.GatewayAuthenticated;
        BishopConnectionId = bishopConnectionId;
        ActiveCharacterId = null;
        ActiveRoleName = null;
        HandoffTicketGuid = null;
        AssignedMapId = null;
        GameServerConnectionId = null;
        IssuedAtUtc = nowUtc;
        TicketExpiresAtUtc = null;
        LastHeartbeatAtUtc = nowUtc;
    }

    public void IssueWorldHandoffTicket(
        Guid characterId,
        string roleName,
        ushort mapId,
        Guid ticketGuid,
        DateTimeOffset nowUtc,
        TimeSpan ticketTtl)
    {
        ActiveCharacterId = characterId;
        ActiveRoleName = roleName;
        AssignedMapId = mapId;
        HandoffTicketGuid = ticketGuid;
        State = SessionLeaseState.HandoffIssued;
        TicketExpiresAtUtc = nowUtc.Add(ticketTtl);
        LastHeartbeatAtUtc = nowUtc;
    }

    public bool ConsumeWorldHandoffTicket(
        Guid presentedTicketGuid,
        DateTimeOffset nowUtc,
        string? gameServerConnectionId = null)
    {
        if (State != SessionLeaseState.HandoffIssued ||
            HandoffTicketGuid is null ||
            HandoffTicketGuid.Value == Guid.Empty ||
            HandoffTicketGuid.Value != presentedTicketGuid ||
            TicketExpiresAtUtc is null ||
            TicketExpiresAtUtc.Value <= nowUtc ||
            ActiveCharacterId is null)
        {
            return false;
        }

        HandoffTicketGuid = null;
        TicketExpiresAtUtc = null;
        GameServerConnectionId = gameServerConnectionId;
        State = SessionLeaseState.InWorld;
        LastHeartbeatAtUtc = nowUtc;
        return true;
    }

    public void RecordHeartbeat(DateTimeOffset nowUtc)
    {
        LastHeartbeatAtUtc = nowUtc;
    }

    public void Release(DateTimeOffset nowUtc)
    {
        State = SessionLeaseState.Offline;
        BishopConnectionId = null;
        GameServerConnectionId = null;
        ActiveCharacterId = null;
        ActiveRoleName = null;
        HandoffTicketGuid = null;
        AssignedMapId = null;
        TicketExpiresAtUtc = null;
        LastHeartbeatAtUtc = nowUtc;
    }
}
