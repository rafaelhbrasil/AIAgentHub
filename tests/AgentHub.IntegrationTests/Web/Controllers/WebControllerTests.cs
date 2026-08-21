using System.Net;
using System.Net.Http.Json;

using AIAgentHub.Domain.Configuration;
using AIAgentHub.Infrastructure.Persistence;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentHub.IntegrationTests.Web.Controllers;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _testDbPath = Path.Combine(Path.GetTempPath(), "AgentHubWebTest_" + Guid.NewGuid().ToString("N") + ".db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _ = builder.UseEnvironment("Testing");
        _ = builder.ConfigureServices(services =>
        {
            var descriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AgentHubDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(AgentHubDbContext)).ToList();

            foreach (var d in descriptors)
            {
                _ = services.Remove(d);
            }

            _ = services.AddDbContext<AgentHubDbContext>(options =>
            {
                _ = options.UseSqlite($"Data Source={_testDbPath}");
                _ = options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            _ = services.Configure<CliExecutionOptions>(options =>
            {
                options.Headless = true;
            });
        });
    }

    public void InitializeDatabase()
    {
        using var scope = Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        initializer.InitializeAsync().GetAwaiter().GetResult();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }
}

public sealed class WebControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public WebControllerTests(CustomWebApplicationFactory factory)
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
    public async Task SetupAndLoginFlow_AllowsAccessToProtectedEndpoints()
    {
        var client = _factory.CreateClient();

        // 1. Initialize admin
        var initRes = await client.PostAsJsonAsync("/api/v1/auth/setup/initialize", new
        {
            username = "admin",
            password = "SecurePassword123!",
            confirmPassword = "SecurePassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, initRes.StatusCode);

        // 2. Cookie session is automatically established
        var drivesRes = await client.GetAsync("/api/v1/filesystem/drives");
        Assert.Equal(HttpStatusCode.OK, drivesRes.StatusCode);

        var providersRes = await client.GetAsync("/api/v1/providers");
        Assert.Equal(HttpStatusCode.OK, providersRes.StatusCode);
    }

    [Fact]
    public async Task RecoverWipe_WithoutRecoveryFlag_ShouldReturnForbidden()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/v1/auth/recover-wipe", null);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    private sealed record SetupStatusResponse(bool IsSetupCompleted, bool IsRecoveryModeEnabled, bool IsLocalRequest, bool CanResetWithoutCode);
}
