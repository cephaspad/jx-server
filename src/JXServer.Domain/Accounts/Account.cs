using JXServer.Primitives.Accounts;
using JXServer.Primitives.Encodings;
using JXServer.Primitives.ErrorCodes;

namespace JXServer.Domain.Accounts;

/// <summary>
/// Aggregate root representing a player account stored in the persistence store.
/// </summary>
public sealed class Account
{
    /// <summary>
    /// Gets the unique account identifier.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the raw account username as submitted (<c>1..31</c> ASCII bytes).
    /// </summary>
    public string UserName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the normalized uppercase account username used for case-insensitive uniqueness and lookup.
    /// </summary>
    public string NormalizedUserName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the 32-character uppercase hexadecimal MD5 digest of the primary password.
    /// </summary>
    public string PasswordHashUpperMd5 { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the optional 32-character uppercase hexadecimal MD5 digest of the secondary character-deletion password.
    /// </summary>
    public string? SecondaryPasswordHashUpperMd5 { get; private set; }

    /// <summary>
    /// Gets the current administrative status of the account.
    /// </summary>
    public AccountStatus Status { get; private set; }

    /// <summary>
    /// Gets the remaining subscription seconds (<c>nLeftTime</c> in <c>KAccountUserReturnExt</c>).
    /// </summary>
    public uint RemainingTimeSeconds { get; private set; }

    /// <summary>
    /// Gets the monotonically increasing fencing token incremented on every new login / session takeover.
    /// </summary>
    public long CurrentLeaseEpoch { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when the account was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp of the most recent successful login.
    /// </summary>
    public DateTimeOffset? LastLoginAtUtc { get; private set; }

    /// <summary>
    /// Gets the optimistic concurrency token.
    /// </summary>
    public Guid ConcurrencyStamp { get; private set; }

    private Account()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Account"/> aggregate root.
    /// </summary>
    public Account(
        Guid id,
        string userName,
        string passwordOrMd5Hex,
        string? secondaryPasswordOrMd5Hex = null,
        AccountStatus status = AccountStatus.Active,
        uint remainingTimeSeconds = 86_400u,
        DateTimeOffset? createdAtUtc = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Account Id cannot be empty.", nameof(id));
        }

        string trimmedUserName = (userName ?? string.Empty).TrimEnd('\0').Trim();
        if (trimmedUserName.Length < AccountConsts.MinUserNameByteLength ||
            trimmedUserName.Length > AccountConsts.MaxUserNameByteLength)
        {
            throw new BusinessException(
                JXServerErrorCodes.AccountInvalidCredentials,
                $"Account username length must be between {AccountConsts.MinUserNameByteLength} and {AccountConsts.MaxUserNameByteLength}.");
        }

        string normalizedHash = KsgPasswordHasher.NormalizeClientPasswordOrHash(passwordOrMd5Hex);
        if (!KsgPasswordHasher.IsHexDigest32(normalizedHash))
        {
            throw new BusinessException(
                JXServerErrorCodes.AccountInvalidCredentials,
                "Primary password hash must be a valid 32-character hexadecimal MD5 digest.");
        }

        Id = id;
        UserName = trimmedUserName;
        NormalizedUserName = NormalizeUserName(trimmedUserName);
        PasswordHashUpperMd5 = normalizedHash;
        SecondaryPasswordHashUpperMd5 = string.IsNullOrWhiteSpace(secondaryPasswordOrMd5Hex)
            ? null
            : KsgPasswordHasher.NormalizeClientPasswordOrHash(secondaryPasswordOrMd5Hex);
        Status = status;
        RemainingTimeSeconds = remainingTimeSeconds;
        CurrentLeaseEpoch = 0;
        CreatedAtUtc = createdAtUtc ?? DateTimeOffset.UtcNow;
        ConcurrencyStamp = Guid.NewGuid();
    }

    /// <summary>
    /// Normalizes a raw username for case-insensitive lookup.
    /// </summary>
    public static string NormalizeUserName(string userName) =>
        (userName ?? string.Empty).TrimEnd('\0').Trim().ToUpperInvariant();

    /// <summary>
    /// Verifies the submitted primary password or MD5 digest against <see cref="PasswordHashUpperMd5"/>.
    /// </summary>
    public bool VerifyPassword(ReadOnlySpan<char> candidateMd5OrPassword) =>
        KsgPasswordHasher.Verify(PasswordHashUpperMd5, candidateMd5OrPassword);

    /// <summary>
    /// Verifies the character deletion password (`FR-006`): checks <see cref="SecondaryPasswordHashUpperMd5"/> if configured;
    /// otherwise accepts a valid primary password or an empty/omitted password on an already-authenticated session.
    /// </summary>
    public bool VerifyDeletionPassword(ReadOnlySpan<char> candidateMd5OrPassword)
    {
        if (!string.IsNullOrEmpty(SecondaryPasswordHashUpperMd5))
        {
            return KsgPasswordHasher.Verify(SecondaryPasswordHashUpperMd5, candidateMd5OrPassword);
        }

        ReadOnlySpan<char> trimmed = candidateMd5OrPassword.TrimEnd('\0').Trim();
        if (trimmed.IsEmpty)
        {
            return true;
        }

        return KsgPasswordHasher.Verify(PasswordHashUpperMd5, trimmed);
    }

    /// <summary>
    /// Advances <see cref="CurrentLeaseEpoch"/> and records the login timestamp.
    /// </summary>
    public long AdvanceLeaseEpoch(DateTimeOffset loginAtUtc)
    {
        CurrentLeaseEpoch++;
        LastLoginAtUtc = loginAtUtc;
        ConcurrencyStamp = Guid.NewGuid();
        return CurrentLeaseEpoch;
    }

    /// <summary>
    /// Freezes the account.
    /// </summary>
    public void Freeze()
    {
        Status = AccountStatus.Frozen;
        ConcurrencyStamp = Guid.NewGuid();
    }

    /// <summary>
    /// Activates the account.
    /// </summary>
    public void Activate()
    {
        Status = AccountStatus.Active;
        ConcurrencyStamp = Guid.NewGuid();
    }
}
