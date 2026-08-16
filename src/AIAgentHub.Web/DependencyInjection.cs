using AIAgentHub.Application.Conversations;
using AIAgentHub.Application.Execution;
using AIAgentHub.Application.FileChanges;
using AIAgentHub.Application.Filesystem;
using AIAgentHub.Application.Providers;
using AIAgentHub.Application.Realtime;
using AIAgentHub.Application.Rendering;
using AIAgentHub.Application.Security;
using AIAgentHub.Application.Workspaces;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Infrastructure.Certificates;
using AIAgentHub.Infrastructure.Cryptography;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Persistence;
using AIAgentHub.Infrastructure.Providers;
using AIAgentHub.Infrastructure.Realtime;
using AIAgentHub.Infrastructure.Snapshots;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using IAppAuthService = AIAgentHub.Application.Security.IAuthenticationService;

namespace AIAgentHub.Web;

public static class DependencyInjection
{
    public static IServiceCollection AddAgentHubServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Data Directory & SQLite DB Configuration
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dataDir = Path.Combine(localAppData, "AIAgentHub", "Data");
        if (!Directory.Exists(dataDir))
        {
            _ = Directory.CreateDirectory(dataDir);
        }

        var dbPath = Path.Combine(dataDir, "AIAgentHub.db");

        _ = services.AddDbContext<AgentHubDbContext>(options =>
        {
            _ = options.UseSqlite($"Data Source={dbPath}");
            _ = options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // 2. Repositories
        _ = services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        _ = services.AddScoped<IConversationRepository, ConversationRepository>();
        _ = services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        _ = services.AddScoped<IServerSettingsRepository, ServerSettingsRepository>();
        _ = services.AddScoped<IFileChangeRepository, FileChangeRepository>();
        _ = services.AddScoped<IFileSnapshotRepository, FileSnapshotRepository>();
        _ = services.AddScoped<IEncryptedSecretRepository, EncryptedSecretRepository>();
        _ = services.AddScoped<ISkillRepository, SkillRepository>();
        _ = services.AddScoped<IMcpServerRepository, McpServerRepository>();
        _ = services.AddScoped<IPermissionRequestRepository, PermissionRequestRepository>();
        _ = services.AddScoped<IProviderModelSettingRepository, ProviderModelSettingRepository>();
        _ = services.AddScoped<IProviderDetectionRecordRepository, ProviderDetectionRecordRepository>();
        _ = services.AddScoped<DatabaseInitializer>();
        _ = services.AddScoped<IDatabaseResetter, DatabaseResetter>();

        // 3. Cryptography & Security
        _ = services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        _ = services.AddSingleton<IMasterKeyProvider, MasterKeyProvider>();
        _ = services.AddSingleton<ISecretEncryptor, AesGcmSecretEncryptor>();
        _ = services.AddSingleton<ICertificateManager, CertificateManager>();

        // 4. Filesystem & Snapshots
        _ = services.AddSingleton<ISystemPathValidator, SystemPathValidator>();
        _ = services.AddSingleton<IFilesystemService, FilesystemService>();
        _ = services.AddScoped<ISnapshotService, LocalDiskSnapshotStore>();

        // 5. Diff Engine
        _ = services.AddSingleton<IDiffEngine, DiffEngine>();

        // 6. Content Renderers
        _ = services.AddSingleton<IContentRenderer, TextContentRenderer>();
        _ = services.AddSingleton<IContentRenderer, MarkdownContentRenderer>();
        _ = services.AddSingleton<IContentRenderer, ImageContentRenderer>();
        _ = services.AddSingleton<IContentRenderer, JsonContentRenderer>();
        _ = services.AddSingleton<IContentRenderer, XmlContentRenderer>();
        _ = services.AddSingleton<IContentRenderer, YamlContentRenderer>();
        _ = services.AddSingleton<IContentRenderingManager, ContentRenderingManager>();

        // 7. Provider Adapters & Manager (including Antigravity CLI / agy)
        _ = services.Configure<CliExecutionOptions>(configuration.GetSection("AgentHub:CliExecution"));
        _ = services.Configure<ProvidersOptions>(configuration.GetSection(ProvidersOptions.SectionName));
        _ = services.AddSingleton<HeadlessProcessExecutor>();
        _ = services.AddSingleton<HeadedProcessExecutor>();
        _ = services.AddSingleton<IProcessExecutor>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<CliExecutionOptions>>().Value;
            return options.Headless
                ? sp.GetRequiredService<HeadlessProcessExecutor>()
                : sp.GetRequiredService<HeadedProcessExecutor>();
        });
        _ = services.AddSingleton<IProvider, AntigravityProvider>();
        _ = services.AddSingleton<IProvider, GeminiCliProvider>();
        _ = services.AddSingleton<IProvider, CodexCliProvider>();
        _ = services.AddSingleton<IProvider, ClaudeCodeProvider>();
        _ = services.AddSingleton<IProvider, OpenCodeProvider>();
        _ = services.AddSingleton<IProviderManager>(sp => new ProviderManager(
            sp.GetServices<IProvider>(),
            () => sp.CreateScope().ServiceProvider.GetRequiredService<IProviderModelSettingRepository>(),
            () => sp.CreateScope().ServiceProvider.GetRequiredService<IProviderDetectionRecordRepository>()
        ));

        // 7b. Prompt Logging
        _ = services.AddSingleton<IPromptLogger, PromptLogger>();

        // 8. Application Services
        _ = services.AddScoped<IWorkspaceService, WorkspaceService>();
        _ = services.AddScoped<IConversationService, ConversationService>();
        _ = services.AddScoped<IFileChangeService, FileChangeService>();
        _ = services.AddScoped<ISetupService, SetupService>();
        _ = services.AddScoped<IAppAuthService, AuthenticationService>();
        _ = services.AddScoped<IPermissionService, PermissionService>();
        _ = services.AddScoped<IExecutionOrchestrator, ExecutionOrchestrator>();

        // 9. Real-time (SignalR)
        _ = services.AddSignalR();
        _ = services.AddScoped<IAgentRealtimeBroadcaster, SignalRAgentRealtimeBroadcaster>();

        // 10. Authentication & Cookie Sessions
        _ = services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "AIAgentHub.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
            });

        _ = services.AddAuthorization();

        return services;
    }
}
