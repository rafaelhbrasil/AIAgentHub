using System.Text.Json.Serialization;

using AIAgentHub.Application.Security;
using AIAgentHub.Infrastructure.Certificates;
using AIAgentHub.Infrastructure.Logging;
using AIAgentHub.Infrastructure.Persistence;
using AIAgentHub.Infrastructure.Realtime;
using AIAgentHub.Web;
using AIAgentHub.Web.Startup;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;

using Scalar.AspNetCore;

// Detect actual wwwroot location across development, IDE, test, and release executions
var currentDir = Directory.GetCurrentDirectory();
var resolvedWebRoot = ResolveWebRoot(currentDir);
var builderOptions = new WebApplicationOptions
{
    Args = args,
    WebRootPath = resolvedWebRoot
};

var builder = WebApplication.CreateBuilder(builderOptions);
_ = builder.Logging.AddFile(builder.Configuration);

ConfigurePortsAndKestrel(builder, args);

var safeClientIp = ParseSafeClientIp(args);
var isRecoveryMode = IsRecoveryMode(args);

builder.Services.AddSingleton(new RecoveryOptions
{
    IsRecoveryModeEnabled = isRecoveryMode,
    SafeClientIp = safeClientIp
});

// Configure services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddAgentHubServices(builder.Configuration);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("AuthLimiter", opt =>
    {
        opt.PermitLimit = 15;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});

var app = builder.Build();

// Database and certificate initialization
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

if (app.Environment.IsDevelopment())
{
    _ = app.MapOpenApi();
    _ = app.MapScalarApiReference();
}

// Security Response Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

ConfigureStaticFiles(app, resolvedWebRoot);

app.UseRouting();

app.UseRateLimiter();

app.UseMiddleware<AIAgentHub.Web.Middleware.NetworkModeMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AgentHubHub>("/hubs/agent");

// SPA Fallback
_ = !string.IsNullOrEmpty(resolvedWebRoot) && File.Exists(Path.Combine(resolvedWebRoot, IndexFileName))
    ? app.MapFallback(async context =>
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(Path.Combine(resolvedWebRoot, IndexFileName));
    }).AllowAnonymous()
    : app.MapFallbackToFile(IndexFileName).AllowAnonymous();

app.Lifetime.ApplicationStarted.Register(() =>
{
    StartupLifecycleHelper.OnApplicationStarted(app.Services, args, builder.Configuration, app.Environment);
});

await app.RunAsync();

// Required for WebApplicationFactory in integration tests
public partial class Program
{
    private const string WebRootFolderName = "wwwroot";
    private const string IndexFileName = "index.html";

    private static string? ResolveWebRoot(string currentDir)
    {
        var candidateWebRoots = new[]
        {
            Path.Combine(AppContext.BaseDirectory, WebRootFolderName),
            Path.Combine(currentDir, WebRootFolderName),
            Path.Combine(currentDir, "src", "AIAgentHub.Web", WebRootFolderName),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", WebRootFolderName))
        };

        return candidateWebRoots.FirstOrDefault(Directory.Exists);
    }

    private static void ConfigurePortsAndKestrel(WebApplicationBuilder builder, string[] args)
    {
        var portArgIndex = Array.FindIndex(args, IsPortFlag);

        if (portArgIndex >= 0 && portArgIndex < args.Length - 1 && int.TryParse(args[portArgIndex + 1], out var customPort))
        {
            _ = builder.WebHost.UseUrls($"https://0.0.0.0:{customPort};http://0.0.0.0:{customPort + 1}");
        }

        if (builder.Environment.IsEnvironment("Testing"))
        {
            return;
        }

        try
        {
            var certManager = new CertificateManager();
            var tlsCert = certManager.GetOrCreateSelfSignedCertificate();

            _ = builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.ConfigureHttpsDefaults(httpsOptions =>
                {
                    httpsOptions.ServerCertificate = tlsCert;
                });
            });
        }
        catch
        {
            // Fallback for restricted test hosts
        }

        if (!HasExplicitUrls(builder, args))
        {
            _ = builder.WebHost.UseUrls("https://0.0.0.0:5432;http://0.0.0.0:5433");
        }
    }

    private static bool IsPortFlag(string arg) =>
        string.Equals(arg, "--port", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "-port", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "/port", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "-p", StringComparison.OrdinalIgnoreCase);

    private static bool HasExplicitUrls(WebApplicationBuilder builder, string[] args) =>
        !string.IsNullOrWhiteSpace(builder.Configuration["urls"]) ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")) ||
        args.Any(a => a.StartsWith("--urls", StringComparison.OrdinalIgnoreCase) ||
                      a.StartsWith("/urls", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(a, "--port", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(a, "-port", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(a, "/port", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(a, "-p", StringComparison.OrdinalIgnoreCase));

    private static string? ParseSafeClientIp(string[] args)
    {
        var safeClientPrefixes = new[] { "--safe-client=", "-safe-client=", "/safe-client=", "--safeclient=", "-safeclient=", "/safeclient=", "--safe-ip=", "-safe-ip=", "/safe-ip=" };
        var safeClientFlagIndex = Array.FindIndex(args, IsSafeClientFlag);

        if (safeClientFlagIndex >= 0 && safeClientFlagIndex < args.Length - 1)
        {
            return args[safeClientFlagIndex + 1]?.Trim();
        }

        var inlineArg = args.FirstOrDefault(a => safeClientPrefixes.Any(p => a.StartsWith(p, StringComparison.OrdinalIgnoreCase)));
        if (inlineArg == null)
        {
            return null;
        }

        var split = inlineArg.Split('=', 2);
        return split.Length == 2 ? split[1]?.Trim() : null;
    }

    private static bool IsSafeClientFlag(string arg) =>
        string.Equals(arg, "--safe-client", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "-safe-client", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "/safe-client", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "--safeclient", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "-safeclient", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "/safeclient", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "--safe-ip", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "-safe-ip", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "/safe-ip", StringComparison.OrdinalIgnoreCase);

    private static bool IsRecoveryMode(string[] args) =>
        args.Any(a =>
            string.Equals(a, "--recovery", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "-recovery", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "/recovery", StringComparison.OrdinalIgnoreCase));

    private static void ConfigureStaticFiles(WebApplication app, string? resolvedWebRoot)
    {
        if (!string.IsNullOrEmpty(resolvedWebRoot) && Directory.Exists(resolvedWebRoot))
        {
            var fileProvider = new PhysicalFileProvider(resolvedWebRoot);
            var defaultFileOptions = new DefaultFilesOptions { FileProvider = fileProvider };
            defaultFileOptions.DefaultFileNames.Clear();
            defaultFileOptions.DefaultFileNames.Add(IndexFileName);
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
    }
}
