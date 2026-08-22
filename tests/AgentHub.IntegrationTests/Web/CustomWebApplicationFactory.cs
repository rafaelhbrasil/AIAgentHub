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
