using AIAgentHub.Domain.Security;

namespace AgentHub.UnitTests.Domain.Security;

public sealed class UserAccountTests
{
    [Fact]
    public void UserAccount_And_EncryptedSecret()
    {
        var user = UserAccount.Create("admin", "hash123", "salt123", "recHash123");
        Assert.Equal("admin", user.Username);
        Assert.Equal("hash123", user.PasswordHash);
        Assert.Equal("salt123", user.PasswordSalt);
        Assert.Equal("recHash123", user.RecoveryCodeHash);
        Assert.Null(user.LastLoginAtUtc);

        user.RecordLogin();
        _ = Assert.NotNull(user.LastLoginAtUtc);

        user.UpdatePassword("newHash", "newSalt");
        Assert.Equal("newHash", user.PasswordHash);
        Assert.Equal("newSalt", user.PasswordSalt);

        _ = Assert.Throws<ArgumentException>(() => UserAccount.Create("", "h", "s", "r"));

        var secret = new EncryptedSecret
        {
            ProviderId = "opencode",
            KeyName = "API_KEY",
            CiphertextBase64 = "cipher",
            NonceBase64 = "nonce",
            TagBase64 = "tag"
        };
        Assert.Equal("opencode", secret.ProviderId);
        Assert.Equal("API_KEY", secret.KeyName);
        Assert.Equal("cipher", secret.CiphertextBase64);
    }

    [Fact]
    public void UserAccount_Lockout_ThreeAttempts_LocksFor10Minutes()
    {
        var user = UserAccount.Create("admin", "hash", "salt", "rec");
        var now = DateTimeOffset.UtcNow;

        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.False(user.IsLockedOut(now));

        // Attempt 1
        user.RecordFailedLogin(now);
        Assert.Equal(1, user.FailedLoginAttempts);
        Assert.False(user.IsLockedOut(now));

        // Attempt 2
        user.RecordFailedLogin(now);
        Assert.Equal(2, user.FailedLoginAttempts);
        Assert.False(user.IsLockedOut(now));

        // Attempt 3: Locks out
        user.RecordFailedLogin(now);
        Assert.Equal(3, user.FailedLoginAttempts);
        Assert.True(user.IsLockedOut(now));
        Assert.True(user.IsLockedOut(now.AddMinutes(9)));
        Assert.False(user.IsLockedOut(now.AddMinutes(11)));

        // Login resets
        user.RecordLogin();
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockoutEndUtc);
        Assert.False(user.IsLockedOut(now));
    }
}
