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

        if (!_passwordHasher.VerifyPassword(password, admin.PasswordHash, admin.PasswordSalt))
        {
            return new AuthResult(false, null, "Invalid username or password.");
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
}
