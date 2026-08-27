using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

using AIAgentHub.Application.Security;
using AIAgentHub.Infrastructure.Certificates;
using AIAgentHub.Infrastructure.Persistence;
using AIAgentHub.Infrastructure.Realtime;
using AIAgentHub.Web;
using AIAgentHub.Web.Startup;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;

using Scalar.AspNetCore;

// Detect actual wwwroot location across development, IDE, test, and release executions
var currentDir = Directory.GetCurrentDirectory();
var candidateWebRoots = new[]
{
    Path.Combine(AppContext.BaseDirectory, "wwwroot"),
    Path.Combine(currentDir, "wwwroot"),
    Path.Combine(currentDir, "src", "AIAgentHub.Web", "wwwroot"),
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "wwwroot"))
};

var resolvedWebRoot = candidateWebRoots.FirstOrDefault(Directory.Exists);
var builderOptions = new WebApplicationOptions
{
    Args = args,
    WebRootPath = resolvedWebRoot
};

var builder = WebApplication.CreateBuilder(builderOptions);

// Parse custom --port flag if provided
var portArgIndex = Array.FindIndex(args, a =>
    string.Equals(a, "--port", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(a, "-port", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(a, "/port", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(a, "-p", StringComparison.OrdinalIgnoreCase));

if (portArgIndex >= 0 && portArgIndex < args.Length - 1 && int.TryParse(args[portArgIndex + 1], out var customPort))
{
    _ = builder.WebHost.UseUrls($"https://0.0.0.0:{customPort};http://0.0.0.0:{customPort + 1}");
}

// Configure Kestrel TLS certificate & default ports 5432 (HTTPS) and 5433 (HTTP)
if (!builder.Environment.IsEnvironment("Testing"))
{
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

    var hasExplicitUrls = !string.IsNullOrWhiteSpace(builder.Configuration["urls"]) ||
                          !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")) ||
                          args.Any(a => a.StartsWith("--urls", StringComparison.OrdinalIgnoreCase) ||
                                        a.StartsWith("/urls", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(a, "--port", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(a, "-port", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(a, "/port", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(a, "-p", StringComparison.OrdinalIgnoreCase));

    if (!hasExplicitUrls)
    {
        _ = builder.WebHost.UseUrls("https://0.0.0.0:5432;http://0.0.0.0:5433");
    }
}

// Parse --recovery flag from command-line parameters
var isRecoveryMode = args.Any(a =>
    string.Equals(a, "--recovery", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(a, "-recovery", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(a, "/recovery", StringComparison.OrdinalIgnoreCase));

builder.Services.AddSingleton(new RecoveryOptions { IsRecoveryModeEnabled = isRecoveryMode });

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

// Static files configuration
if (!string.IsNullOrEmpty(resolvedWebRoot) && Directory.Exists(resolvedWebRoot))
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

app.UseMiddleware<AIAgentHub.Web.Middleware.NetworkModeMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AgentHubHub>("/hubs/agent");

// SPA Fallback
_ = !string.IsNullOrEmpty(resolvedWebRoot) && File.Exists(Path.Combine(resolvedWebRoot, "index.html"))
    ? app.MapFallback(async context =>
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(Path.Combine(resolvedWebRoot, "index.html"));
    }).AllowAnonymous()
    : app.MapFallbackToFile("index.html").AllowAnonymous();

app.Lifetime.ApplicationStarted.Register(() =>
{
    StartupLifecycleHelper.OnApplicationStarted(app.Services, args, builder.Configuration, app.Environment);
});

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
