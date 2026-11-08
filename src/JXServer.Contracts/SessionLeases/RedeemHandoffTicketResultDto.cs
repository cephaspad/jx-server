using JXServer.Contracts.Characters;

namespace JXServer.Contracts.SessionLeases;

/// <summary>
/// Result DTO returned from <see cref="ISessionLeaseAppService.RedeemWorldHandoffTicketAsync"/>.
/// </summary>
public sealed record RedeemHandoffTicketResultDto(
    bool Succeeded,
    long LeaseEpoch,
    CharacterDto? Character);
