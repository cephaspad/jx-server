namespace JXServer.Primitives.Accounts;

/// <summary>
/// Validation and fixed-buffer length constants for player and bot accounts.
/// </summary>
public static class AccountConsts
{
    public const int MinUserNameByteLength = 1;
    public const int MaxUserNameByteLength = 31;
    public const int FixedAccountBufferSize = 32;
    public const int PasswordMd5HexLength = 32;
    public const int FixedPasswordBufferSize = 64;
    public const uint RequiredProtocolVersion = 0x00000041u;
}
