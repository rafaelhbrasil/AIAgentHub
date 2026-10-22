using System.Net;
using System.Net.Http.Json;

namespace AgentHub.IntegrationTests.Web.Auth;

public sealed class AuthPasswordResetIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthPasswordResetIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.InitializeDatabase();
    }

    [Fact]
    public async Task ResetWithPassword_WrongPasswordRejects_CorrectPasswordWipesAndSignsOut()
    {
        var client = _factory.CreateClient();

        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "admin",
            password = "123456"
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        // 1. Wrong password: rejected, nothing destroyed, session intact.
        var wrongRes = await client.PostAsJsonAsync("/api/v1/auth/reset-with-password", new
        {
            password = "WrongPassword999!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, wrongRes.StatusCode);

        var sessionAfterWrong = await client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.OK, sessionAfterWrong.StatusCode);

        var statusAfterWrong = await client.GetAsync("/api/v1/auth/setup/status");
        var statusBody = await statusAfterWrong.Content.ReadFromJsonAsync<SetupStatusResponse>();
        Assert.NotNull(statusBody);
        Assert.True(statusBody.IsSetupCompleted);

        // 2. Correct password: full wipe, setup mode, signed out.
        var resetRes = await client.PostAsJsonAsync("/api/v1/auth/reset-with-password", new
        {
            password = "123456"
        });
        Assert.Equal(HttpStatusCode.OK, resetRes.StatusCode);

        var sessionAfterReset = await client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, sessionAfterReset.StatusCode);

        var statusAfterReset = await client.GetAsync("/api/v1/auth/setup/status");
        var resetStatus = await statusAfterReset.Content.ReadFromJsonAsync<SetupStatusResponse>();
        Assert.NotNull(resetStatus);
        Assert.False(resetStatus.IsSetupCompleted);

        // 3. Old admin no longer exists (full wipe).
        var reloginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "admin",
            password = "123456"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, reloginRes.StatusCode);
    }

    [Fact]
    public async Task ResetWithPassword_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/v1/auth/reset-with-password", new
        {
            password = "123456"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    private sealed record SetupStatusResponse(bool IsSetupCompleted, bool IsRecoveryModeEnabled, bool IsLocalRequest, bool CanResetWithoutCode);
}
