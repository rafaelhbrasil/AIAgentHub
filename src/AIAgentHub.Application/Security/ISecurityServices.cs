using AIAgentHub.Domain.Security;

namespace AIAgentHub.Application.Security;

public sealed record SetupResult(bool Success, string? RecoveryCode, string? Error = null);

public sealed record AuthResult(bool Success, UserAccount? Account, string? Error = null);

public interface IPasswordHasher
{
    public (string HashBase64, string SaltBase64) HashPassword(string password);
    public bool VerifyPassword(string password, string hashBase64, string saltBase64);
    public (string HashBase64, string PlainCode) GenerateRecoveryCode();
    public bool VerifyRecoveryCode(string plainCode, string hashBase64);
}

public interface ISecretEncryptor
{
    public (string CiphertextBase64, string NonceBase64, string TagBase64) Encrypt(string plainSecret);
    public string Decrypt(string ciphertextBase64, string nonceBase64, string tagBase64);
}

public sealed class RecoveryOptions
{
    public bool IsRecoveryModeEnabled { get; set; }
}

public interface IDatabaseResetter
{
    public Task WipeAllDataAsync(CancellationToken cancellationToken = default);
}

public interface ISetupService
{
    public Task<bool> IsSetupCompletedAsync(CancellationToken cancellationToken = default);
    public Task<SetupResult> InitializeAdminAsync(string username, string password, string confirmPassword, CancellationToken cancellationToken = default);
    public Task<bool> ValidateRecoveryCodeAsync(string recoveryCode, CancellationToken cancellationToken = default);
    public Task<bool> ResetToSetupModeAsync(string? recoveryCode, CancellationToken cancellationToken = default);
    public Task<bool> WipeAllDataAsync(CancellationToken cancellationToken = default);
}

public interface IAuthenticationService
{
    public Task<AuthResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    public Task<UserAccount?> GetAdminAsync(CancellationToken cancellationToken = default);
    public Task<string?> GetAdminUsernameAsync(CancellationToken cancellationToken = default);
}
