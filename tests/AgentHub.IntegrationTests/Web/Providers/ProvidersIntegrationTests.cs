using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIAgentHub.Domain.Providers;
using Xunit;

namespace AgentHub.IntegrationTests.Web.Providers;

public sealed class ProvidersIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CustomWebApplicationFactory _factory;

    public ProvidersIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.InitializeDatabase();
    }

    [Fact]
    public async Task GetAllProviders_WhenAuthenticated_ReturnsCachedListWithAllProviders()
    {
        var client = await SetupAndAuthenticateClientAsync();

        // 1. Initial Call (seeds cache if not seeded)
        var res = await client.GetAsync("/api/v1/providers");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var providers = await res.Content.ReadFromJsonAsync<List<ProviderInfo>>(JsonOpts);
        Assert.NotNull(providers);
        Assert.NotEmpty(providers);

        // Verify all 5 providers exist
        Assert.Contains(providers, p => p.Id == "antigravity");
        Assert.Contains(providers, p => p.Id == "claude");
        Assert.Contains(providers, p => p.Id == "codex");
        Assert.Contains(providers, p => p.Id == "gemini");
        Assert.Contains(providers, p => p.Id == "opencode");

        // 2. Second Call (must be fast and read from cache)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cachedRes = await client.GetAsync("/api/v1/providers");
        sw.Stop();

        Assert.Equal(HttpStatusCode.OK, cachedRes.StatusCode);
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Expected cached call to take < 2000ms, took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task RefreshStream_ReturnsServerSentEventsStream()
    {
        var client = await SetupAndAuthenticateClientAsync();

        var res = await client.GetAsync("/api/v1/providers/refresh-stream", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/event-stream", res.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UpdateModelSettings_TogglesVisibilityAndPersists()
    {
        var client = await SetupAndAuthenticateClientAsync();

        // 1. Get models for antigravity provider
        var modelsRes = await client.GetAsync("/api/v1/providers/antigravity/models");
        Assert.Equal(HttpStatusCode.OK, modelsRes.StatusCode);
        var models = await modelsRes.Content.ReadFromJsonAsync<List<ModelInfo>>(JsonOpts);
        Assert.NotNull(models);
        Assert.NotEmpty(models);

        var targetModelId = models.First().Id;

        // 2. Toggle model setting to false
        var updatePayload = new
        {
            modelStates = new Dictionary<string, bool>
            {
                [targetModelId] = false
            }
        };
        var putRes = await client.PutAsJsonAsync("/api/v1/providers/antigravity/models/settings", updatePayload);
        Assert.Equal(HttpStatusCode.OK, putRes.StatusCode);

        // 3. Verify state persisted in subsequent call
        var verifyRes = await client.GetAsync("/api/v1/providers/antigravity/models");
        var updatedModels = await verifyRes.Content.ReadFromJsonAsync<List<ModelInfo>>(JsonOpts);
        Assert.NotNull(updatedModels);
        var updatedModel = updatedModels.FirstOrDefault(m => m.Id == targetModelId);
        Assert.NotNull(updatedModel);
        Assert.False(updatedModel.IsDisplayed);

        // 4. Restore state to true
        var restorePayload = new
        {
            modelStates = new Dictionary<string, bool>
            {
                [targetModelId] = true
            }
        };
        var restoreRes = await client.PutAsJsonAsync("/api/v1/providers/antigravity/models/settings", restorePayload);
        Assert.Equal(HttpStatusCode.OK, restoreRes.StatusCode);
    }

    private async Task<HttpClient> SetupAndAuthenticateClientAsync()
    {
        var client = _factory.CreateClient();
        var initRes = await client.PostAsJsonAsync("/api/v1/auth/setup/initialize", new
        {
            username = "admin",
            password = "SecurePassword123!",
            confirmPassword = "SecurePassword123!"
        });
        if (!initRes.IsSuccessStatusCode)
        {
            _ = await client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                username = "admin",
                password = "SecurePassword123!"
            });
        }
        return client;
    }
}
