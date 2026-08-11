using AIAgentHub.Domain.Security;

namespace AIAgentHub.Application.Security;

public sealed record SetupResult(bool Success, string? RecoveryCode, string? Error = null);

public sealed record AuthResult(bool Success, UserAccount? Account, string? Error = null);

public interface IPasswordHasher
{
    (string HashBase64, string SaltBase64) HashPassword(string password);
    bool VerifyPassword(string password, string hashBase64, string saltBase64);
    (string HashBase64, string PlainCode) GenerateRecoveryCode();
    bool VerifyRecoveryCode(string plainCode, string hashBase64);
}

public interface ISecretEncryptor
{
    (string CiphertextBase64, string NonceBase64, string TagBase64) Encrypt(string plainSecret);
    string Decrypt(string ciphertextBase64, string nonceBase64, string tagBase64);
}

public sealed class RecoveryOptions
{
    public bool IsRecoveryModeEnabled { get; set; }
}

public interface IDatabaseResetter
{
    Task WipeAllDataAsync(CancellationToken cancellationToken = default);
}

public interface ISetupService
{
    Task<bool> IsSetupCompletedAsync(CancellationToken cancellationToken = default);
    Task<SetupResult> InitializeAdminAsync(string username, string password, string confirmPassword, CancellationToken cancellationToken = default);
    Task<bool> ValidateRecoveryCodeAsync(string recoveryCode, CancellationToken cancellationToken = default);
    Task<bool> ResetToSetupModeAsync(string? recoveryCode, CancellationToken cancellationToken = default);
    Task<bool> WipeAllDataAsync(CancellationToken cancellationToken = default);
}

public interface IAuthenticationService
{
    Task<AuthResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<UserAccount?> GetAdminAsync(CancellationToken cancellationToken = default);
    Task<string?> GetAdminUsernameAsync(CancellationToken cancellationToken = default);
}
