using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace AgentHub.IntegrationTests.Web.Auth;

public sealed class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.InitializeDatabase();
    }

    [Fact]
    public async Task GetSetupStatus_ShouldReturnJson_AnonymousAllowed()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/v1/auth/setup/status");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<SetupStatusResponse>();
        Assert.NotNull(body);
        Assert.False(body.IsRecoveryModeEnabled);
        Assert.False(body.CanResetWithoutCode);
    }

    [Fact]
    public async Task ProtectedEndpoints_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        var client = _factory.CreateClient();

        var drivesRes = await client.GetAsync("/api/v1/filesystem/drives");
        Assert.Equal(HttpStatusCode.Unauthorized, drivesRes.StatusCode);

        var providersRes = await client.GetAsync("/api/v1/providers");
        Assert.Equal(HttpStatusCode.Unauthorized, providersRes.StatusCode);

        var workspacesRes = await client.GetAsync("/api/v1/workspaces");
        Assert.Equal(HttpStatusCode.Unauthorized, workspacesRes.StatusCode);

        var settingsRes = await client.GetAsync("/api/v1/settings");
        Assert.Equal(HttpStatusCode.Unauthorized, settingsRes.StatusCode);
    }

    [Fact]
    public async Task FullAuthLifecycle_Setup_Me_Logout_Login_Recover()
    {
        var client = _factory.CreateClient();

        // 1. Initial Setup
        var initRes = await client.PostAsJsonAsync("/api/v1/auth/setup/initialize", new
        {
            username = "admin",
            password = "SecurePassword123!",
            confirmPassword = "SecurePassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, initRes.StatusCode);
        var initBody = await initRes.Content.ReadFromJsonAsync<SetupInitResponse>();
        Assert.NotNull(initBody);
        Assert.True(initBody.Success);
        Assert.False(string.IsNullOrEmpty(initBody.RecoveryCode));

        // 2. Session / Me endpoint
        var sessionRes = await client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.OK, sessionRes.StatusCode);
        var sessionBody = await sessionRes.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(sessionBody);
        Assert.True(sessionBody.IsAuthenticated);
        Assert.Equal("admin", sessionBody.Username);

        // 3. Logout
        var logoutRes = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logoutRes.StatusCode);

        // 4. Verify unauthenticated after logout
        var afterLogoutSession = await client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogoutSession.StatusCode);

        // 5. Login with invalid password
        var badLoginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "admin",
            password = "WrongPassword999!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, badLoginRes.StatusCode);

        // 6. Login with correct password
        var goodLoginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "admin",
            password = "SecurePassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, goodLoginRes.StatusCode);

        // 7. Recover with recovery code
        var recoverRes = await client.PostAsJsonAsync("/api/v1/auth/recover", new
        {
            recoveryCode = initBody.RecoveryCode
        });
        Assert.Equal(HttpStatusCode.OK, recoverRes.StatusCode);

        // 8. Verify setup status is now not completed (system reset to setup mode)
        var statusAfterReset = await client.GetAsync("/api/v1/auth/setup/status");
        var resetStatus = await statusAfterReset.Content.ReadFromJsonAsync<SetupStatusResponse>();
        Assert.NotNull(resetStatus);
        Assert.False(resetStatus.IsSetupCompleted);
    }

    [Fact]
    public async Task RecoverWipe_WithoutRecoveryFlag_ShouldReturnForbidden()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/v1/auth/recover-wipe", null);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    private sealed record SetupStatusResponse(bool IsSetupCompleted, bool IsRecoveryModeEnabled, bool IsLocalRequest, bool CanResetWithoutCode);
    private sealed record SetupInitResponse(bool Success, string? RecoveryCode, string? Message);
    private sealed record SessionResponse(bool IsAuthenticated, string Username, DateTimeOffset? LastLoginAtUtc);
}
