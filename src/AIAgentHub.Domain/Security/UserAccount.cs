using AIAgentHub.Domain.Common;

namespace AIAgentHub.Domain.Security;

public sealed class UserAccount : AggregateRoot
{
    public string Username { get; private set; } = "admin";
    public string PasswordHash { get; private set; } = string.Empty;
    public string PasswordSalt { get; private set; } = string.Empty;
    public string RecoveryCodeHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAtUtc { get; private set; }
    public int FailedLoginAttempts { get; private set; } = 0;
    public DateTimeOffset? LockoutEndUtc { get; private set; }

    private UserAccount() { }

    public static UserAccount Create(string username, string passwordHash, string passwordSalt, string recoveryCodeHash)
    {
        return string.IsNullOrWhiteSpace(username)
            ? throw new ArgumentException("Username cannot be empty.", nameof(username))
            : new UserAccount
            {
                Id = Guid.NewGuid(),
                Username = username.Trim(),
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                RecoveryCodeHash = recoveryCodeHash,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
    }

    public bool IsLockedOut(DateTimeOffset? now = null)
    {
        var current = now ?? DateTimeOffset.UtcNow;
        return LockoutEndUtc.HasValue && LockoutEndUtc.Value > current;
    }

    public void RecordFailedLogin(DateTimeOffset? now = null)
    {
        var current = now ?? DateTimeOffset.UtcNow;
        if (LockoutEndUtc.HasValue && LockoutEndUtc.Value <= current)
        {
            FailedLoginAttempts = 0;
            LockoutEndUtc = null;
        }

        FailedLoginAttempts++;
        if (FailedLoginAttempts >= 3)
        {
            LockoutEndUtc = current.AddMinutes(10);
        }
    }

    public void RecordLogin()
    {
        LastLoginAtUtc = DateTimeOffset.UtcNow;
        FailedLoginAttempts = 0;
        LockoutEndUtc = null;
    }

    public void ResetLockout()
    {
        FailedLoginAttempts = 0;
        LockoutEndUtc = null;
    }

    public void UpdatePassword(string passwordHash, string passwordSalt)
    {
        PasswordHash = passwordHash;
        PasswordSalt = passwordSalt;
        FailedLoginAttempts = 0;
        LockoutEndUtc = null;
    }
}

public sealed class EncryptedSecret : Entity
{
    public string ProviderId { get; set; } = string.Empty;
    public string KeyName { get; set; } = string.Empty;
    public string CiphertextBase64 { get; set; } = string.Empty;
    public string NonceBase64 { get; set; } = string.Empty;
    public string TagBase64 { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
