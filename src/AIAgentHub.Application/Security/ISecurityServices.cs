using AIAgentHub.Domain.Security;

namespace AIAgentHub.Application.Security;

public sealed record SetupResult(bool Success, string? RecoveryCode, string? Error = null);

public sealed record AuthResult(bool Success, UserAccount? Account, string? Error = null, bool IsLockedOut = false);

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
    public string? SafeClientIp { get; set; }

    public bool IsSafeClient(System.Net.IPAddress? remoteIp)
    {
        if (remoteIp == null || string.IsNullOrWhiteSpace(SafeClientIp))
        {
            return false;
        }

        if (remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        if (System.Net.IPAddress.TryParse(SafeClientIp.Trim(), out var parsedSafeIp))
        {
            if (parsedSafeIp.IsIPv4MappedToIPv6)
            {
                parsedSafeIp = parsedSafeIp.MapToIPv4();
            }

            return parsedSafeIp.Equals(remoteIp);
        }

        return false;
    }

    public bool IsSafeClientOrLoopback(System.Net.IPAddress? remoteIp)
    {
        if (remoteIp == null || System.Net.IPAddress.IsLoopback(remoteIp))
        {
            return true;
        }

        return IsSafeClient(remoteIp);
    }
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
