using System.IO;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Security;
using AIAgentHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentHub.IntegrationTests.Web.Chat;

public class LiveProviderWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _testDbPath = Path.Combine(Path.GetTempPath(), "AgentHubLiveTest_" + Guid.NewGuid().ToString("N") + ".db");

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

            // Note: We deliberately do NOT replace IProcessExecutor here.
            // The real HeadlessProcessExecutor remains registered to execute actual installed CLI binaries.
        });
    }

    public async Task InitializeDatabaseAndAdminAsync()
    {
        using var scope = Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync();

        var settingsRepo = scope.ServiceProvider.GetRequiredService<IServerSettingsRepository>();
        var settings = await settingsRepo.GetAsync();
        // LAN MODE DISABLED: Localhost only
        settings.NetworkMode = NetworkMode.Localhost;
        await settingsRepo.UpdateAsync(settings);
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
