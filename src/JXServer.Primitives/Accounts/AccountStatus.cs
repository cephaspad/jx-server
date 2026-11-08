namespace JXServer.Primitives.Accounts;

/// <summary>
/// Access state of a registered account.
/// </summary>
public enum AccountStatus : byte
{
    Active = 0,
    Frozen = 1,
    Locked = 2
}
