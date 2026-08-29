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

        var hubRes = await client.GetAsync("/hubs/agent");
        Assert.Equal(HttpStatusCode.Unauthorized, hubRes.StatusCode);
    }

    [Fact]
    public async Task ProtectedPageRoutes_WhenUnauthenticatedHtmlRequest_ShouldServeSpaFallback()
    {
        var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/providers");
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FullAuthLifecycle_Setup_Me_Logout_Login_Recover()
    {
        var client = _factory.CreateClient();

        // 1. Login with correct password
        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "admin",
            password = "123456"
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

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

        // 6. Login with correct password again
        var goodLoginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "admin",
            password = "123456"
        });
        Assert.Equal(HttpStatusCode.OK, goodLoginRes.StatusCode);
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
