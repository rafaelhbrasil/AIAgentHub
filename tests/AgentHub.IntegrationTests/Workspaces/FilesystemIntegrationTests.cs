using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentHub.IntegrationTests.Web;
using AIAgentHub.Application.Filesystem;
using Xunit;

namespace AgentHub.IntegrationTests.Workspaces;

public sealed class FilesystemIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FilesystemIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.InitializeDatabase();
    }

    [Fact]
    public async Task Filesystem_DrivesAndForbiddenPaths_ReturnsValidData()
    {
        var client = await SetupAndAuthenticateClientAsync();

        // 1. Drives endpoint
        var drivesRes = await client.GetAsync("/api/v1/filesystem/drives");
        Assert.Equal(HttpStatusCode.OK, drivesRes.StatusCode);
        var drives = await drivesRes.Content.ReadFromJsonAsync<List<DriveItem>>(JsonOpts);
        Assert.NotNull(drives);
        Assert.NotEmpty(drives);

        // 2. Forbidden paths endpoint
        var forbiddenRes = await client.GetAsync("/api/v1/filesystem/forbidden-paths");
        Assert.Equal(HttpStatusCode.OK, forbiddenRes.StatusCode);
        var forbiddenDoc = await forbiddenRes.Content.ReadFromJsonAsync<JsonDocument>(JsonOpts);
        Assert.NotNull(forbiddenDoc);
        Assert.True(forbiddenDoc.RootElement.TryGetProperty("forbiddenPaths", out var pathsProp));
        Assert.NotEmpty(pathsProp.EnumerateArray().ToList());
    }

    [Fact]
    public async Task Filesystem_BrowseAndMkdir_CreatesDirectorySafely()
    {
        var client = await SetupAndAuthenticateClientAsync();

        var tempBase = Path.Combine(Path.GetTempPath(), "AgentHubFsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempBase);
        try
        {
            // 1. Browse base directory
            var browseRes = await client.GetAsync($"/api/v1/filesystem/browse?path={Uri.EscapeDataString(tempBase)}");
            Assert.Equal(HttpStatusCode.OK, browseRes.StatusCode);
            var browseResult = await browseRes.Content.ReadFromJsonAsync<DirectoryBrowseResult>(JsonOpts);
            Assert.NotNull(browseResult);
            Assert.Equal(tempBase, browseResult.CurrentPath);

            // 2. Create subfolder via mkdir
            var subfolderPath = Path.Combine(tempBase, "nested_folder");
            var mkdirRes = await client.PostAsJsonAsync("/api/v1/filesystem/mkdir", new { path = subfolderPath });
            Assert.Equal(HttpStatusCode.OK, mkdirRes.StatusCode);

            // Verify folder was created on disk
            Assert.True(Directory.Exists(subfolderPath));

            // 3. Browse again and verify subfolder appears in entries
            var browseAfterRes = await client.GetAsync($"/api/v1/filesystem/browse?path={Uri.EscapeDataString(tempBase)}");
            var browseAfter = await browseAfterRes.Content.ReadFromJsonAsync<DirectoryBrowseResult>(JsonOpts);
            Assert.NotNull(browseAfter);
            Assert.Contains(browseAfter.Entries, d => d.Name == "nested_folder" && d.IsDirectory);
        }
        finally
        {
            if (Directory.Exists(tempBase))
            {
                try { Directory.Delete(tempBase, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task Filesystem_Mkdir_WhenPathInvalidOrForbidden_ReturnsBadRequest()
    {
        var client = await SetupAndAuthenticateClientAsync();

        // 1. Empty path
        var emptyRes = await client.PostAsJsonAsync("/api/v1/filesystem/mkdir", new { path = "" });
        Assert.Equal(HttpStatusCode.BadRequest, emptyRes.StatusCode);

        // 2. System32 or forbidden directory
        var forbiddenPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (!string.IsNullOrEmpty(forbiddenPath))
        {
            var forbiddenRes = await client.PostAsJsonAsync("/api/v1/filesystem/mkdir", new { path = Path.Combine(forbiddenPath, "test_dir") });
            Assert.Equal(HttpStatusCode.BadRequest, forbiddenRes.StatusCode);
        }
    }

    private async Task<HttpClient> SetupAndAuthenticateClientAsync()
    {
        var client = _factory.CreateClient();
        var setupStatusRes = await client.GetAsync("/api/v1/auth/setup/status");
        var setupStatus = await setupStatusRes.Content.ReadFromJsonAsync<SetupStatusResponse>(JsonOpts);

        if (setupStatus?.IsSetupCompleted != true)
        {
            _ = await client.PostAsJsonAsync("/api/v1/auth/setup/initialize", new
            {
                username = "admin",
                password = "123456",
                confirmPassword = "123456"
            });
        }

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

        return client;
    }

    private sealed record SetupStatusResponse(bool IsSetupCompleted, bool IsRecoveryModeEnabled, bool IsLocalRequest, bool CanResetWithoutCode);
}
