using AIAgentHub.Application.Common;
using AIAgentHub.Application.Security;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Security;

namespace AgentHub.UnitTests.Application.Security;

public sealed class SetupServiceTests
{
    private sealed class FakeUserRepo : IUserAccountRepository
    {
        private UserAccount? _admin;
        public FakeUserRepo(UserAccount? admin = null) => _admin = admin;
        public Task<UserAccount?> GetAdminAsync(CancellationToken cancellationToken = default) => Task.FromResult(_admin);
        public Task AddAsync(UserAccount account, CancellationToken cancellationToken = default)
        {
            _admin = account;
            return Task.CompletedTask;
        }
        public Task UpdateAsync(UserAccount account, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAllAsync(CancellationToken cancellationToken = default)
        {
            _admin = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSettingsRepo : IServerSettingsRepository
    {
        public ServerSettings Settings { get; } = new();
        public Task<ServerSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Settings);
        public Task UpdateAsync(ServerSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public (string HashBase64, string SaltBase64) HashPassword(string password) => ("hash", "salt");
        public bool VerifyPassword(string password, string hashBase64, string saltBase64) => true;
        public (string HashBase64, string PlainCode) GenerateRecoveryCode() => ("RECOVERY_HASH", "AAAA-BBBB-CCCC-DDDD");
        public bool VerifyRecoveryCode(string plainCode, string hashBase64) => plainCode.Replace("-", "").ToUpperInvariant() == "AAAABBBBCCCCDDDD" && hashBase64 == "RECOVERY_HASH";
    }

    [Fact]
    public async Task SetupService_WipeAllDataAsync_ShouldInvokeDatabaseResetter()
    {
        var resetter = new TestDatabaseResetter();
        var setupService = new SetupService(null!, null!, null!, resetter);

        var result = await setupService.WipeAllDataAsync();

        Assert.True(result);
        Assert.True(resetter.WasWiped);
    }

    [Fact]
    public async Task SetupService_ResetToSetupModeAsync_WithInvalidRecoveryCode_ShouldFail()
    {
        var admin = UserAccount.Create("admin", "pwd_hash", "salt", "RECOVERY_HASH");
        var userRepo = new FakeUserRepo(admin);
        var settingsRepo = new FakeSettingsRepo();
        var hasher = new FakePasswordHasher();
        var resetter = new TestDatabaseResetter();

        var service = new SetupService(settingsRepo, userRepo, hasher, resetter);

        var result = await service.ResetToSetupModeAsync("WRONG-CODE-1111-2222");

        Assert.False(result);
        Assert.NotNull(await userRepo.GetAdminAsync());
    }

    [Fact]
    public async Task SetupService_ResetToSetupModeAsync_WithValidRecoveryCode_ShouldSucceedAndWipeAdmin()
    {
        var admin = UserAccount.Create("admin", "pwd_hash", "salt", "RECOVERY_HASH");
        var userRepo = new FakeUserRepo(admin);
        var settingsRepo = new FakeSettingsRepo();
        var hasher = new FakePasswordHasher();
        var resetter = new TestDatabaseResetter();

        var service = new SetupService(settingsRepo, userRepo, hasher, resetter);

        var result = await service.ResetToSetupModeAsync("AAAA-BBBB-CCCC-DDDD");

        Assert.True(result);
        Assert.Null(await userRepo.GetAdminAsync());
        Assert.False(settingsRepo.Settings.IsSetupCompleted);
    }

    private sealed class TestDatabaseResetter : IDatabaseResetter
    {
        public bool WasWiped { get; private set; }
        public Task WipeAllDataAsync(CancellationToken cancellationToken = default)
        {
            WasWiped = true;
            return Task.CompletedTask;
        }
    }
}
