using JXServer.Primitives.Accounts;
using Microsoft.Extensions.Logging;

namespace JXServer.Application.Logging;

/// <summary>
/// High-performance source-generated log messages for <c>JXServer.Application</c>.
/// </summary>
public static partial class ApplicationLogMessages
{
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Account '{UserName}' authentication completed with Result={ResultCode}, AccountId={AccountId}, LeaseEpoch={LeaseEpoch}.")]
    public static partial void LogAccountAuthenticated(
        ILogger logger,
        string userName,
        LoginResultCode resultCode,
        Guid? accountId,
        long leaseEpoch);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Issued world handoff ticket {TicketGuid} for AccountId={AccountId}, Role='{RoleName}', MapId={MapId}, Epoch={LeaseEpoch}.")]
    public static partial void LogHandoffTicketIssued(
        ILogger logger,
        Guid ticketGuid,
        Guid accountId,
        string roleName,
        ushort mapId,
        long leaseEpoch);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Information,
        Message = "Redeemed world handoff ticket {TicketGuid} for AccountId={AccountId}, Role='{RoleName}', Epoch={LeaseEpoch}.")]
    public static partial void LogHandoffTicketRedeemed(
        ILogger logger,
        Guid ticketGuid,
        Guid accountId,
        string roleName,
        long leaseEpoch);
}
