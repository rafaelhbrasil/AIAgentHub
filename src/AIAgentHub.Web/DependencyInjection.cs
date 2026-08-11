using AIAgentHub.Application.Conversations;
using AIAgentHub.Application.Execution;
using AIAgentHub.Application.FileChanges;
using AIAgentHub.Application.Filesystem;
using AIAgentHub.Application.Providers;
using AIAgentHub.Application.Realtime;
using AIAgentHub.Application.Rendering;
using AIAgentHub.Application.Security;
using AIAgentHub.Application.Workspaces;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Infrastructure.Certificates;
using AIAgentHub.Infrastructure.Cryptography;
using AIAgentHub.Infrastructure.Persistence;
using AIAgentHub.Infrastructure.Providers;
using AIAgentHub.Infrastructure.Realtime;
using AIAgentHub.Infrastructure.Snapshots;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using IAppAuthService = AIAgentHub.Application.Security.IAuthenticationService;

namespace AIAgentHub.Web;

public static class DependencyInjection
{
    public static IServiceCollection AddAgentHubServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Data Directory & SQLite DB Configuration
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dataDir = Path.Combine(localAppData, "AIAgentHub", "Data");
        if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "AIAgentHub.db");

        services.AddDbContext<AgentHubDbContext>(options =>
        {
            options.UseSqlite($"Data Source={dbPath}");
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // 2. Repositories
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IServerSettingsRepository, ServerSettingsRepository>();
        services.AddScoped<IFileChangeRepository, FileChangeRepository>();
        services.AddScoped<IFileSnapshotRepository, FileSnapshotRepository>();
        services.AddScoped<IEncryptedSecretRepository, EncryptedSecretRepository>();
        services.AddScoped<ISkillRepository, SkillRepository>();
        services.AddScoped<IMcpServerRepository, McpServerRepository>();
        services.AddScoped<IPermissionRequestRepository, PermissionRequestRepository>();
        services.AddScoped<IProviderModelSettingRepository, ProviderModelSettingRepository>();
        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<IDatabaseResetter, DatabaseResetter>();

        // 3. Cryptography & Security
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<IMasterKeyProvider, MasterKeyProvider>();
        services.AddSingleton<ISecretEncryptor, AesGcmSecretEncryptor>();
        services.AddSingleton<ICertificateManager, CertificateManager>();

        // 4. Filesystem & Snapshots
        services.AddSingleton<IFilesystemService, FilesystemService>();
        services.AddScoped<ISnapshotService, LocalDiskSnapshotStore>();

        // 5. Diff Engine
        services.AddSingleton<IDiffEngine, DiffEngine>();

        // 6. Content Renderers
        services.AddSingleton<IContentRenderer, TextContentRenderer>();
        services.AddSingleton<IContentRenderer, MarkdownContentRenderer>();
        services.AddSingleton<IContentRenderer, ImageContentRenderer>();
        services.AddSingleton<IContentRenderer, JsonContentRenderer>();
        services.AddSingleton<IContentRenderer, XmlContentRenderer>();
        services.AddSingleton<IContentRenderer, YamlContentRenderer>();
        services.AddSingleton<IContentRenderingManager, ContentRenderingManager>();

        // 7. Provider Adapters & Manager (including Antigravity CLI / agy)
        services.Configure<AIAgentHub.Domain.Configuration.CliExecutionOptions>(configuration.GetSection("AgentHub:CliExecution"));
        services.AddSingleton<IProvider, AntigravityProvider>();
        services.AddSingleton<IProvider, GeminiCliProvider>();
        services.AddSingleton<IProvider, CodexCliProvider>();
        services.AddSingleton<IProvider, ClaudeCodeProvider>();
        services.AddSingleton<IProvider, OpenCodeProvider>();
        services.AddSingleton<IProviderManager>(sp => new ProviderManager(
            sp.GetServices<IProvider>(),
            () => sp.CreateScope().ServiceProvider.GetRequiredService<IProviderModelSettingRepository>()
        ));

        // 8. Application Services
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IFileChangeService, FileChangeService>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<IAppAuthService, AIAgentHub.Application.Security.AuthenticationService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IExecutionOrchestrator, ExecutionOrchestrator>();

        // 9. Real-time (SignalR)
        services.AddSignalR();
        services.AddScoped<IAgentRealtimeBroadcaster, SignalRAgentRealtimeBroadcaster>();

        // 10. Authentication & Cookie Sessions
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
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

        services.AddAuthorization();

        return services;
    }
}
