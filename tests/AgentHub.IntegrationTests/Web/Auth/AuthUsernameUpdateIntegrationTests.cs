using System.Net;
using System.Net.Http.Json;

namespace AgentHub.IntegrationTests.Web.Auth;

public sealed class AuthUsernameUpdateIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthUsernameUpdateIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.InitializeDatabase();
    }

    [Fact]
    public async Task UpdateUsername_WithoutPassword_SucceedsAndRefreshesSession()
    {
        var client = _factory.CreateClient();

        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "admin",
            password = "123456"
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        // No currentPassword in payload: username change must be authorized by session alone.
        var updateRes = await client.PutAsJsonAsync("/api/v1/auth/account/username", new
        {
            newUsername = "admin_renamed"
        });
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var sessionRes = await client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.OK, sessionRes.StatusCode);
        var session = await sessionRes.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);
        Assert.Equal("admin_renamed", session.Username);
    }

    private sealed record SessionResponse(bool IsAuthenticated, string Username, DateTimeOffset? LastLoginAtUtc);
}
