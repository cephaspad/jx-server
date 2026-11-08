namespace JXServer.Primitives.ErrorCodes;

/// <summary>
/// Structured ABP-style domain error codes (<c>JXServer:&lt;Domain&gt;:&lt;Code&gt;</c>).
/// </summary>
public static class JXServerErrorCodes
{
    public const string AccountInvalidCredentials = "JXServer:Account:0001";
    public const string AccountFrozenOrLocked = "JXServer:Account:0002";
    public const string AccountAlreadyOnline = "JXServer:Account:0003";
    public const string AccountInvalidProtocolVersion = "JXServer:Account:0004";

    public const string CharacterLimitReached = "JXServer:Character:0001";
    public const string CharacterNameDuplicate = "JXServer:Character:0002";
    public const string CharacterNameProhibited = "JXServer:Character:0003";
    public const string CharacterInvalidStarterVillage = "JXServer:Character:0004";
    public const string CharacterNotFound = "JXServer:Character:0005";

    public const string SessionHandoffTicketInvalidOrExpired = "JXServer:Session:0001";
    public const string SessionEpochSuperseded = "JXServer:Session:0002";
}
