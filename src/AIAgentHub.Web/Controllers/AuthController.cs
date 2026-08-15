using System.Security.Claims;

using AIAgentHub.Application.Security;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using IAppAuthService = AIAgentHub.Application.Security.IAuthenticationService;

namespace AIAgentHub.Web.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    ISetupService setupService,
    IAppAuthService authService,
    RecoveryOptions recoveryOptions) : ControllerBase
{
    private readonly ISetupService _setupService = setupService;
    private readonly IAppAuthService _authService = authService;
    private readonly RecoveryOptions _recoveryOptions = recoveryOptions;

    [HttpGet("setup/status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSetupStatus(CancellationToken cancellationToken)
    {
        var isCompleted = await _setupService.IsSetupCompletedAsync(cancellationToken);
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        var isLocalRequest = remoteIp == null || System.Net.IPAddress.IsLoopback(remoteIp);
        var isRecoveryEnabled = _recoveryOptions.IsRecoveryModeEnabled;
        var canResetWithoutCode = isRecoveryEnabled && isLocalRequest;

        return Ok(new
        {
            isSetupCompleted = isCompleted,
            isRecoveryModeEnabled = isRecoveryEnabled,
            isLocalRequest,
            canResetWithoutCode
        });
    }

    public sealed record SetupInitRequest(string Username, string Password, string ConfirmPassword);

    [HttpPost("setup/initialize")]
    [AllowAnonymous]
    public async Task<IActionResult> InitializeSetup([FromBody] SetupInitRequest request, CancellationToken cancellationToken)
    {
        var result = await _setupService.InitializeAdminAsync(request.Username, request.Password, request.ConfirmPassword, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { code = "setup_failed", message = result.Error });
        }

        // Automatically sign in the user upon setup completion
        await SignInUserAsync(request.Username);

        return Ok(new
        {
            success = true,
            recoveryCode = result.RecoveryCode,
            message = "Administrator account created and authenticated."
        });
    }

    [HttpPost("setup/reset")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetSetup(CancellationToken cancellationToken)
    {
        var isCompleted = await _setupService.IsSetupCompletedAsync(cancellationToken);
        if (isCompleted)
        {
            var remoteIp = HttpContext.Connection.RemoteIpAddress;
            var isLocalRequest = remoteIp == null || System.Net.IPAddress.IsLoopback(remoteIp);
            if (!_recoveryOptions.IsRecoveryModeEnabled || !isLocalRequest)
            {
                return Forbid();
            }
        }

        _ = await _setupService.ResetToSetupModeAsync(null, cancellationToken);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { success = true, message = "System reset to Setup Mode." });
    }

    public sealed record LoginRequest(string Username, string Password);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request.Username, request.Password, cancellationToken);
        if (!result.Success || result.Account == null)
        {
            return Unauthorized(new { code = "invalid_credentials", message = result.Error ?? "Invalid username or password." });
        }

        await SignInUserAsync(result.Account.Username);

        return Ok(new
        {
            success = true,
            username = result.Account.Username,
            lastLoginAtUtc = result.Account.LastLoginAtUtc
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { success = true });
    }

    [HttpGet("session")]
    public async Task<IActionResult> GetSession(CancellationToken cancellationToken)
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized();
        }

        var admin = await _authService.GetAdminAsync(cancellationToken);
        return Ok(new
        {
            isAuthenticated = true,
            username = admin?.Username ?? User.Identity?.Name ?? "admin",
            lastLoginAtUtc = admin?.LastLoginAtUtc
        });
    }

    public sealed record RecoverRequest(string RecoveryCode);

    [HttpPost("recover")]
    [AllowAnonymous]
    public async Task<IActionResult> RecoverPassword([FromBody] RecoverRequest request, CancellationToken cancellationToken)
    {
        var success = await _setupService.ResetToSetupModeAsync(request.RecoveryCode, cancellationToken);
        if (!success)
        {
            return BadRequest(new { code = "invalid_recovery_code", message = "Invalid recovery code." });
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { success = true, message = "System reset to Setup Mode successfully." });
    }

    [HttpPost("recover-wipe")]
    [AllowAnonymous]
    public async Task<IActionResult> RecoverWipe(CancellationToken cancellationToken)
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        var isLocalRequest = remoteIp == null || System.Net.IPAddress.IsLoopback(remoteIp);

        if (!_recoveryOptions.IsRecoveryModeEnabled || !isLocalRequest)
        {
            return Forbid();
        }

        _ = await _setupService.WipeAllDataAsync(cancellationToken);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return Ok(new { success = true, message = "Database forcefully wiped and system reset to Setup Mode." });
    }

    private async Task SignInUserAsync(string username)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, "Administrator")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
    }
}
