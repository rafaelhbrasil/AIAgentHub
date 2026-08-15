using System.Text.Json.Serialization;

using AIAgentHub.Application.Security;
using AIAgentHub.Infrastructure.Certificates;
using AIAgentHub.Infrastructure.Persistence;
using AIAgentHub.Infrastructure.Realtime;
using AIAgentHub.Web;

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

// Configure Kestrel to bind default ports 5432 (HTTPS) and 5433 (HTTP)
if (!builder.Environment.IsEnvironment("Testing"))
{
    _ = builder.WebHost.ConfigureKestrel((context, serverOptions) =>
    {
        try
        {
            var certManager = new CertificateManager();
            var tlsCert = certManager.GetOrCreateSelfSignedCertificate();

            serverOptions.ListenAnyIP(5432, listenOptions =>
            {
                _ = listenOptions.UseHttps(tlsCert);
            });

            serverOptions.ListenAnyIP(5433);
        }
        catch
        {
            // Fallback for restricted test hosts
        }
    });
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
    })
    : app.MapFallbackToFile("index.html");

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
