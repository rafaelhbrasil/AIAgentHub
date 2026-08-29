using System.IO;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Persistence;
using AIAgentHub.Infrastructure.Providers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentHub.IntegrationTests.Web;

public sealed class TestProcessExecutor : IProcessExecutor
{
    public async Task ExecuteAsync(
        string displayName,
        string executableName,
        string arguments,
        ProviderExecutionContext context,
        IPromptLogger promptLogger,
        CliExecutionOptions options)
    {
        await context.OnStreamToken($"[{displayName}] Simulated response for: {context.Prompt}");
        if (context.OnSessionCreated != null && string.IsNullOrEmpty(context.ProviderSessionId))
        {
            await context.OnSessionCreated($"session-{context.ConversationId}");
        }
    }

    public Task<ProcessCommandResult> RunCommandAsync(
        string executable,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default,
        string? operationTitle = null)
    {
        return Task.FromResult(new ProcessCommandResult(0, "mock-output", string.Empty));
    }

    public bool AbortProcess(Guid conversationId) => true;
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
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

            var executorDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IProcessExecutor));
            if (executorDescriptor != null)
            {
                _ = services.Remove(executorDescriptor);
            }
            _ = services.AddSingleton<IProcessExecutor, TestProcessExecutor>();
        });
    }

    private readonly object _initLock = new();
    private bool _initialized;

    public void InitializeDatabase()
    {
        lock (_initLock)
        {
            if (_initialized) return;

            using var scope = Services.CreateScope();
            var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            initializer.InitializeAsync().GetAwaiter().GetResult();

            var detectionRepo = scope.ServiceProvider.GetRequiredService<AIAgentHub.Domain.Repositories.IProviderDetectionRecordRepository>();
            var readyProviders = new[] { "antigravity", "claude", "codex", "opencode", "copilot" };
            foreach (var p in readyProviders)
            {
                detectionRepo.UpsertAsync(new AIAgentHub.Domain.Providers.ProviderDetectionRecord
                {
                    ProviderId = p,
                    Status = AIAgentHub.Domain.Providers.ProviderStatus.Ready,
                    StatusDetails = "Provider is operational and ready to use.",
                    IsInstalled = true,
                    IsAuthenticated = true,
                    Version = "1.0.0",
                    ExecutablePath = $"C:\\Tools\\{p}.exe",
                    DetectedAtUtc = DateTimeOffset.UtcNow
                }).GetAwaiter().GetResult();
            }

            detectionRepo.UpsertAsync(new AIAgentHub.Domain.Providers.ProviderDetectionRecord
            {
                ProviderId = "gemini",
                Status = AIAgentHub.Domain.Providers.ProviderStatus.Discontinued,
                StatusDetails = "Discontinued client.",
                IsInstalled = true,
                IsAuthenticated = true,
                Version = "0.55.1",
                DetectedAtUtc = DateTimeOffset.UtcNow
            }).GetAwaiter().GetResult();

            var setupService = scope.ServiceProvider.GetRequiredService<AIAgentHub.Application.Security.ISetupService>();
            if (!setupService.IsSetupCompletedAsync().GetAwaiter().GetResult())
            {
                _ = setupService.InitializeAdminAsync("admin", "123456", "123456").GetAwaiter().GetResult();
            }

            _initialized = true;
        }
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
