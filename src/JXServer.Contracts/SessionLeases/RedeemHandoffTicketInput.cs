namespace JXServer.Contracts.SessionLeases;

/// <summary>
/// Input DTO for redeeming a 16-byte handoff ticket on the GameServer (<c>0x42</c> <c>c2s_logiclogin</c>).
/// </summary>
public sealed record RedeemHandoffTicketInput(
    Guid TicketGuid,
    string? GameServerConnectionId = null);
