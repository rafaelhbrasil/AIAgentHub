using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Security;

namespace AIAgentHub.Application.Security;

public sealed class SetupService(
    IServerSettingsRepository settingsRepository,
    IUserAccountRepository userRepository,
    IPasswordHasher passwordHasher,
    IDatabaseResetter databaseResetter) : ISetupService
{
    private readonly IServerSettingsRepository _settingsRepository = settingsRepository;
    private readonly IUserAccountRepository _userRepository = userRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IDatabaseResetter _databaseResetter = databaseResetter;

    public async Task<bool> IsSetupCompletedAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsRepository.GetAsync(cancellationToken);
        var admin = await _userRepository.GetAdminAsync(cancellationToken);
        return settings.IsSetupCompleted && admin != null;
    }

    public async Task<SetupResult> InitializeAdminAsync(string username, string password, string confirmPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return new SetupResult(false, null, "Username cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            return new SetupResult(false, null, "Password must be at least 6 characters long.");
        }

        if (password != confirmPassword)
        {
            return new SetupResult(false, null, "Passwords do not match.");
        }

        var existingAdmin = await _userRepository.GetAdminAsync(cancellationToken);
        if (existingAdmin != null)
        {
            var currentSettings = await _settingsRepository.GetAsync(cancellationToken);
            if (currentSettings.IsSetupCompleted)
            {
                return new SetupResult(false, null, "Setup has already been completed.");
            }
        }

        var (passwordHash, salt) = _passwordHasher.HashPassword(password);
        var (recoveryCodeHash, plainRecoveryCode) = _passwordHasher.GenerateRecoveryCode();

        var admin = UserAccount.Create(username.Trim(), passwordHash, salt, recoveryCodeHash);
        await _userRepository.AddAsync(admin, cancellationToken);

        var settings = await _settingsRepository.GetAsync(cancellationToken);
        settings.IsSetupCompleted = true;
        await _settingsRepository.UpdateAsync(settings, cancellationToken);

        return new SetupResult(true, plainRecoveryCode, null);
    }

    public async Task<bool> ValidateRecoveryCodeAsync(string recoveryCode, CancellationToken cancellationToken = default)
    {
        var admin = await _userRepository.GetAdminAsync(cancellationToken);
        return admin != null && !string.IsNullOrWhiteSpace(admin.RecoveryCodeHash) && _passwordHasher.VerifyRecoveryCode(recoveryCode, admin.RecoveryCodeHash);
    }

    public async Task<bool> ResetToSetupModeAsync(string? recoveryCode, CancellationToken cancellationToken = default)
    {
        var admin = await _userRepository.GetAdminAsync(cancellationToken);
        if (admin != null && !string.IsNullOrWhiteSpace(recoveryCode))
        {
            if (!_passwordHasher.VerifyRecoveryCode(recoveryCode, admin.RecoveryCodeHash))
            {
                return false;
            }
        }

        // Wipe existing admin and reset setup status
        await _userRepository.DeleteAllAsync(cancellationToken);

        var settings = await _settingsRepository.GetAsync(cancellationToken);
        settings.IsSetupCompleted = false;
        await _settingsRepository.UpdateAsync(settings, cancellationToken);

        return true;
    }

    public async Task<bool> WipeAllDataAsync(CancellationToken cancellationToken = default)
    {
        await _databaseResetter.WipeAllDataAsync(cancellationToken);
        return true;
    }
}
