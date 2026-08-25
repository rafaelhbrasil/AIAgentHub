using System.IO;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AIAgentHub.Application.Providers;
using AIAgentHub.Application.Security;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Repositories;
using AIAgentHub.Domain.Security;
using AIAgentHub.Infrastructure.Certificates;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Persistence;
using AIAgentHub.Infrastructure.Providers;
using AIAgentHub.Infrastructure.Realtime;
using AIAgentHub.Web;
using AIAgentHub.Web.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using Xunit;

namespace AgentHub.IntegrationTests.Web.Playwright;

public class PlaywrightTestFixture : IAsyncLifetime
{
    private IHost? _host;
    private readonly string _testDbPath = Path.Combine(Path.GetTempPath(), "AgentHubPlaywright_" + Guid.NewGuid().ToString("N") + ".db");
    public const int HttpsPort = 5432;
    public string ServerAddress => $"https://127.0.0.1:{HttpsPort}";

    public IPlaywright PlaywrightInstance { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // 0. Resolve web root path
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        if (!File.Exists(Path.Combine(projectRoot, "AIAgentHub.slnx")))
        {
            projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        }
        var resolvedWebRoot = Path.Combine(projectRoot, "src", "AIAgentHub.Web", "wwwroot");

        // 1. Build and start Kestrel host
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            WebRootPath = resolvedWebRoot,
            ContentRootPath = resolvedWebRoot
        });

        // LAN MODE DISABLED: Bind strictly to loopback (127.0.0.1)
        _ = builder.WebHost.UseKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, HttpsPort, listenOptions =>
            {
                var certManager = new CertificateManager();
                listenOptions.UseHttps(certManager.GetOrCreateSelfSignedCertificate());
            });
        });
        _ = builder.WebHost.UseUrls(ServerAddress);

        // Required Program-level services
        _ = builder.Services.AddSingleton(new RecoveryOptions { IsRecoveryModeEnabled = false });
        _ = builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("AuthLimiter", opt =>
            {
                opt.PermitLimit = 100;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueLimit = 0;
            });
        });

        // Configure Controllers & AgentHub Services
        _ = builder.Services.AddControllers()
            .AddApplicationPart(typeof(AIAgentHub.Web.Controllers.AuthController).Assembly)
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        _ = builder.Services.AddAgentHubServices(builder.Configuration);

        // Replace DB with temporary SQLite
        var descriptors = builder.Services.Where(d =>
            d.ServiceType == typeof(DbContextOptions<AgentHubDbContext>) ||
            d.ServiceType == typeof(DbContextOptions) ||
            d.ServiceType == typeof(AgentHubDbContext)).ToList();

        foreach (var d in descriptors)
        {
            _ = builder.Services.Remove(d);
        }

        _ = builder.Services.AddDbContext<AgentHubDbContext>(options =>
        {
            _ = options.UseSqlite($"Data Source={_testDbPath}");
            _ = options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        _ = builder.Services.Configure<CliExecutionOptions>(options =>
        {
            options.Headless = true;
        });

        var executorDescriptor = builder.Services.FirstOrDefault(d => d.ServiceType == typeof(IProcessExecutor));
        if (executorDescriptor != null)
        {
            _ = builder.Services.Remove(executorDescriptor);
        }
        _ = builder.Services.AddSingleton<IProcessExecutor, TestProcessExecutor>();

        // Build app
        var app = builder.Build();

        if (Directory.Exists(resolvedWebRoot))
        {
            var fileProvider = new PhysicalFileProvider(resolvedWebRoot);
            var defaultFileOptions = new DefaultFilesOptions { FileProvider = fileProvider };
            defaultFileOptions.DefaultFileNames.Clear();
            defaultFileOptions.DefaultFileNames.Add("index.html");
            _ = app.UseDefaultFiles(defaultFileOptions);

            _ = app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = fileProvider,
                RequestPath = ""
            });
        }
        else
        {
            _ = app.UseDefaultFiles();
            _ = app.UseStaticFiles();
        }

        app.UseRouting();
        app.UseRateLimiter();
        app.UseMiddleware<NetworkModeMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHub<AgentHubHub>("/hubs/agent");

        if (Directory.Exists(resolvedWebRoot) && File.Exists(Path.Combine(resolvedWebRoot, "index.html")))
        {
            app.MapFallback(async context =>
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.SendFileAsync(Path.Combine(resolvedWebRoot, "index.html"));
            });
        }
        else
        {
            app.MapFallbackToFile("index.html");
        }

        _host = app;
        await _host.StartAsync();

        // 2. Database & Admin Setup
        using (var scope = app.Services.CreateScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            await initializer.InitializeAsync();

            var settingsRepo = scope.ServiceProvider.GetRequiredService<IServerSettingsRepository>();
            var settings = await settingsRepo.GetAsync();
            // LAN MODE DISABLED: Ensure NetworkMode is strictly Localhost
            settings.NetworkMode = NetworkMode.Localhost;
            await settingsRepo.UpdateAsync(settings);

            var setupService = scope.ServiceProvider.GetRequiredService<ISetupService>();
            if (!await setupService.IsSetupCompletedAsync())
            {
                _ = await setupService.InitializeAdminAsync("admin", "123456", "123456");
            }
        }

        // 3. Playwright browser setup
        try
        {
            _ = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        }
        catch { }

        PlaywrightInstance = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task<IPage> CreatePageAsync()
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            BaseURL = ServerAddress
        });
        var page = await context.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER CONSOLE {msg.Type}]: {msg.Text}");
        page.PageError += (_, err) => Console.WriteLine($"[BROWSER PAGE ERROR]: {err}");
        return page;
    }

    public async Task DisposeAsync()
    {
        if (Browser != null)
        {
            await Browser.CloseAsync();
        }
        PlaywrightInstance?.Dispose();

        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }
}

[CollectionDefinition("PlaywrightCollection", DisableParallelization = true)]
public class PlaywrightCollection : ICollectionFixture<PlaywrightTestFixture> { }

public static class PlaywrightTestHelper
{
    public static async Task LoginIfRequiredAsync(IPage page)
    {
        _ = await page.WaitForSelectorAsync("#root", new PageWaitForSelectorOptions { Timeout = 10000 });

        var loginInput = page.Locator("#loginUsername");
        var dashboardTab = page.Locator("[data-tab=\"dashboard\"]");

        for (var i = 0; i < 20; i++)
        {
            if (await dashboardTab.IsVisibleAsync())
            {
                return;
            }

            if (await loginInput.IsVisibleAsync())
            {
                await page.FillAsync("#loginUsername", "admin");
                await page.FillAsync("#loginPassword", "123456");
                await page.ClickAsync("#loginSubmitBtn");
                _ = await page.WaitForSelectorAsync("[data-tab=\"dashboard\"]", new PageWaitForSelectorOptions { Timeout = 10000 });
                return;
            }

            await page.WaitForTimeoutAsync(250);
        }

        _ = await page.WaitForSelectorAsync("[data-tab=\"dashboard\"]", new PageWaitForSelectorOptions { Timeout = 10000 });
    }
}
