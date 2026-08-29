using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIAgentHub.Domain.Security;
using Xunit;

namespace AgentHub.IntegrationTests.Web.Settings;

public sealed class SettingsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CustomWebApplicationFactory _factory;

    public SettingsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.InitializeDatabase();
    }

    [Fact]
    public async Task SettingsAndDiagnostics_WhenAuthenticated_ReturnsSuccess()
    {
        var client = await SetupAndAuthenticateClientAsync();

        // 1. Get Settings
        var getRes = await client.GetAsync("/api/v1/settings");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var settings = await getRes.Content.ReadFromJsonAsync<ServerSettings>(JsonOpts);
        Assert.NotNull(settings);

        // 2. Update Settings
        var updatePayload = new
        {
            networkMode = NetworkMode.Localhost,
            listeningPortHttps = 5432,
            listeningPortHttp = 5000,
            selectedInterfaces = new List<string>(),
            theme = "dark"
        };
        var putRes = await client.PutAsJsonAsync($"/api/v1/settings/{settings.Id}", updatePayload);
        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);
        var updated = await putRes.Content.ReadFromJsonAsync<ServerSettings>(JsonOpts);
        Assert.NotNull(updated);
        Assert.Equal("dark", updated.Theme);

        // 3. Network Interfaces
        var nicsRes = await client.GetAsync("/api/v1/settings/network-interfaces");
        Assert.Equal(HttpStatusCode.OK, nicsRes.StatusCode);

        // 4. Recovery Code Info
        var recRes = await client.GetAsync("/api/v1/settings/recovery-code");
        Assert.Equal(HttpStatusCode.OK, recRes.StatusCode);

        // 5. Filesystem Drives
        var drivesRes = await client.GetAsync("/api/v1/filesystem/drives");
        Assert.Equal(HttpStatusCode.OK, drivesRes.StatusCode);
        var drives = await drivesRes.Content.ReadFromJsonAsync<List<AIAgentHub.Application.Filesystem.DriveItem>>(JsonOpts);
        Assert.NotNull(drives);
        Assert.NotEmpty(drives);

        // 6. Skills and MCPs
        var skillsRes = await client.GetAsync("/api/v1/skills");
        Assert.Equal(HttpStatusCode.OK, skillsRes.StatusCode);

        var mcpsRes = await client.GetAsync("/api/v1/mcps");
        Assert.Equal(HttpStatusCode.OK, mcpsRes.StatusCode);
    }

    private async Task<HttpClient> SetupAndAuthenticateClientAsync()
    {
        var client = _factory.CreateClient();
        var initRes = await client.PostAsJsonAsync("/api/v1/auth/setup/initialize", new
        {
            username = "admin",
            password = "123456",
            confirmPassword = "123456"
        });
        if (!initRes.IsSuccessStatusCode)
        {
            var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                username = "admin",
                password = "123456"
            });
            if (!loginRes.IsSuccessStatusCode)
            {
                _ = await client.PostAsJsonAsync("/api/v1/auth/login", new
                {
                    username = "admin",
                    password = "123123"
                });
            }
        }
        return client;
    }
}
