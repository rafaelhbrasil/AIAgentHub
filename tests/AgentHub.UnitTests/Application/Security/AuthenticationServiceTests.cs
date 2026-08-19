using AIAgentHub.Application.Security;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Security;

namespace AgentHub.UnitTests.Application.Security;

public sealed class AuthenticationServiceTests
{
    private sealed class FakeUserRepo : IUserAccountRepository
    {
        public UserAccount? Admin { get; set; }

        public Task<UserAccount?> GetAdminAsync(CancellationToken cancellationToken = default) => Task.FromResult(Admin);

        public Task AddAsync(UserAccount account, CancellationToken cancellationToken = default)
        {
            Admin = account;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(UserAccount account, CancellationToken cancellationToken = default)
        {
            Admin = account;
            return Task.CompletedTask;
        }

        public Task DeleteAllAsync(CancellationToken cancellationToken = default)
        {
            Admin = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public (string HashBase64, string SaltBase64) HashPassword(string password) => ("correct_hash", "salt");

        public bool VerifyPassword(string password, string hashBase64, string saltBase64) => password == "CorrectPassword123!";

        public (string HashBase64, string PlainCode) GenerateRecoveryCode() => ("rec_hash", "REC-123456");

        public bool VerifyRecoveryCode(string plainCode, string hashBase64) => plainCode == "REC-123456";
    }

    [Fact]
    public async Task LoginAsync_ThreeConsecutiveWrongPasswords_LocksAccountFor10Minutes()
    {
        var userRepo = new FakeUserRepo
        {
            Admin = UserAccount.Create("admin", "correct_hash", "salt", "rec_hash")
        };
        var hasher = new FakeHasher();
        var authService = new AuthenticationService(userRepo, hasher);

        // 1st wrong attempt
        var result1 = await authService.LoginAsync("admin", "wrong1");
        Assert.False(result1.Success);
        Assert.False(result1.IsLockedOut);
        Assert.Contains("2 attempt(s) remaining", result1.Error);
        Assert.Equal(1, userRepo.Admin.FailedLoginAttempts);
        Assert.False(userRepo.Admin.IsLockedOut());

        // 2nd wrong attempt
        var result2 = await authService.LoginAsync("admin", "wrong2");
        Assert.False(result2.Success);
        Assert.False(result2.IsLockedOut);
        Assert.Contains("1 attempt(s) remaining", result2.Error);
        Assert.Equal(2, userRepo.Admin.FailedLoginAttempts);
        Assert.False(userRepo.Admin.IsLockedOut());

        // 3rd wrong attempt -> Locks account
        var result3 = await authService.LoginAsync("admin", "wrong3");
        Assert.False(result3.Success);
        Assert.True(result3.IsLockedOut);
        Assert.Contains("temporarily locked", result3.Error);
        Assert.Equal(3, userRepo.Admin.FailedLoginAttempts);
        Assert.True(userRepo.Admin.IsLockedOut());

        // 4th attempt while locked (even with correct password) -> Denied immediately
        var result4 = await authService.LoginAsync("admin", "CorrectPassword123!");
        Assert.False(result4.Success);
        Assert.True(result4.IsLockedOut);
        Assert.Contains("temporarily locked", result4.Error);
    }

    [Fact]
    public async Task LoginAsync_SuccessfulLogin_ResetsFailedAttempts()
    {
        var userRepo = new FakeUserRepo
        {
            Admin = UserAccount.Create("admin", "correct_hash", "salt", "rec_hash")
        };
        var hasher = new FakeHasher();
        var authService = new AuthenticationService(userRepo, hasher);

        // 2 wrong attempts
        _ = await authService.LoginAsync("admin", "wrong1");
        _ = await authService.LoginAsync("admin", "wrong2");
        Assert.Equal(2, userRepo.Admin.FailedLoginAttempts);

        // 3rd attempt is correct
        var successResult = await authService.LoginAsync("admin", "CorrectPassword123!");
        Assert.True(successResult.Success);
        Assert.False(successResult.IsLockedOut);
        Assert.Equal(0, userRepo.Admin.FailedLoginAttempts);
        Assert.Null(userRepo.Admin.LockoutEndUtc);
    }
}
