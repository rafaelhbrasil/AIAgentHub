using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Security;

namespace AIAgentHub.Application.Security;

public sealed class AuthenticationService(IUserAccountRepository userRepository, IPasswordHasher passwordHasher) : IAuthenticationService
{
    private readonly IUserAccountRepository _userRepository = userRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;

    public async Task<AuthResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult(false, null, "Username and password are required.");
        }

        var admin = await _userRepository.GetAdminAsync(cancellationToken);
        if (admin == null || !admin.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return new AuthResult(false, null, "Invalid username or password.");
        }

        var now = DateTimeOffset.UtcNow;
        if (admin.IsLockedOut(now))
        {
            var remaining = admin.LockoutEndUtc!.Value - now;
            var remainingMinutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
            return new AuthResult(false, null, $"Account is temporarily locked due to 3 consecutive failed login attempts. Please try again in {remainingMinutes} minute(s).", IsLockedOut: true);
        }

        if (!_passwordHasher.VerifyPassword(password, admin.PasswordHash, admin.PasswordSalt))
        {
            admin.RecordFailedLogin(now);
            await _userRepository.UpdateAsync(admin, cancellationToken);

            if (admin.IsLockedOut(now))
            {
                return new AuthResult(false, null, "Account is temporarily locked due to 3 consecutive failed login attempts. Please try again in 10 minutes.", IsLockedOut: true);
            }

            var remainingAttempts = Math.Max(0, 3 - admin.FailedLoginAttempts);
            return new AuthResult(false, null, $"Invalid username or password. ({remainingAttempts} attempt(s) remaining before a 10-minute lockout)");
        }

        admin.RecordLogin();
        await _userRepository.UpdateAsync(admin, cancellationToken);

        return new AuthResult(true, admin, null);
    }

    public async Task<UserAccount?> GetAdminAsync(CancellationToken cancellationToken = default) => await _userRepository.GetAdminAsync(cancellationToken);

    public async Task<string?> GetAdminUsernameAsync(CancellationToken cancellationToken = default)
    {
        var admin = await _userRepository.GetAdminAsync(cancellationToken);
        return admin?.Username;
    }

    public async Task<UpdateAccountResult> UpdateUsernameAsync(string currentPassword, string newUsername, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            return new UpdateAccountResult(false, "Current password is required.");
        }

        if (string.IsNullOrWhiteSpace(newUsername) || newUsername.Trim().Length < 3)
        {
            return new UpdateAccountResult(false, "New username must be at least 3 characters long.");
        }

        var admin = await _userRepository.GetAdminAsync(cancellationToken);
        if (admin == null)
        {
            return new UpdateAccountResult(false, "Administrator account not found.");
        }

        if (!_passwordHasher.VerifyPassword(currentPassword, admin.PasswordHash, admin.PasswordSalt))
        {
            return new UpdateAccountResult(false, "Current password is incorrect.");
        }

        admin.UpdateUsername(newUsername.Trim());
        await _userRepository.UpdateAsync(admin, cancellationToken);
        return new UpdateAccountResult(true);
    }

    public async Task<UpdateAccountResult> UpdatePasswordAsync(string currentPassword, string newPassword, string confirmNewPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            return new UpdateAccountResult(false, "Current password is required.");
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            return new UpdateAccountResult(false, "New password must be at least 6 characters long.");
        }

        if (!string.Equals(newPassword, confirmNewPassword, StringComparison.Ordinal))
        {
            return new UpdateAccountResult(false, "New password and confirmation do not match.");
        }

        var admin = await _userRepository.GetAdminAsync(cancellationToken);
        if (admin == null)
        {
            return new UpdateAccountResult(false, "Administrator account not found.");
        }

        if (!_passwordHasher.VerifyPassword(currentPassword, admin.PasswordHash, admin.PasswordSalt))
        {
            return new UpdateAccountResult(false, "Current password is incorrect.");
        }

        var (newHash, newSalt) = _passwordHasher.HashPassword(newPassword);
        admin.UpdatePassword(newHash, newSalt);
        await _userRepository.UpdateAsync(admin, cancellationToken);
        return new UpdateAccountResult(true);
    }
}
