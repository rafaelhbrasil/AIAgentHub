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

    public void RecordLogin() => LastLoginAtUtc = DateTimeOffset.UtcNow;

    public void UpdatePassword(string passwordHash, string passwordSalt)
    {
        PasswordHash = passwordHash;
        PasswordSalt = passwordSalt;
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
