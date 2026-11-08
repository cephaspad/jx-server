namespace JXServer.Primitives.Accounts;

/// <summary>
/// Wire result codes returned in <c>s2c_login</c> (<c>0x41</c> at offset <c>+0x03</c>).
/// </summary>
public enum LoginResultCode : uint
{
    Request = 0x00050000u,
    Success = 0x00050001u,
    Failed = 0x00050002u,
    AccountOrPasswordError = 0x00050003u,
    AccountAlreadyOnline = 0x00050004u,
    Timeout = 0x00050005u,
    InvalidProtocolVersion = 0x0005000Au,
    AccountFrozen = 0x0005000Bu
}
