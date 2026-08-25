# Auto-Open Browser on Startup and Console URL Logging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Automatically open the user's default browser to the correct local URL on application startup, unconditionally display listening URLs in the terminal console, and support CLI/appsettings configuration to enable/disable browser launch.

**Architecture:** A dedicated `StartupLifecycleHelper` resolves active Kestrel bound addresses from `IServerAddressesFeature`, normalizes wildcard hosts (`0.0.0.0`, `[::]`, `+`, `*`) to `localhost`, renders a formatted banner directly to `Console.Out`, determines browser launch preferences following CLI > appsettings > default rules, and launches the browser cross-platform when `app.Lifetime.ApplicationStarted` fires.

**Tech Stack:** .NET 10 (C#), ASP.NET Core Kestrel, `Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature`, `System.Diagnostics.Process`, xUnit.

## Global Constraints
- Target Framework: .NET 10 (`net10.0`).
- No new external dependencies (use standard library & ASP.NET Core features).
- Browser launch must be 100% disabled during test runs (`Testing` environment).
- Console banner must write directly to standard output to guarantee visibility regardless of `LogLevel: Warning` or `Error`.
- Respect existing project rules: run tests with dotnet CLI, and never break existing integration/unit tests.

---

### Task 1: Unit Tests for URL Resolution, Banner Formatting, and Configuration Precedence

**Files:**
- Create: `tests/AgentHub.UnitTests/Web/StartupLifecycleHelperTests.cs`

**Interfaces:**
- Produces: Test coverage for `StartupLifecycleHelper.NormalizeUrl`, `StartupLifecycleHelper.ResolveListeningUrls`, `StartupLifecycleHelper.SelectPrimaryBrowserUrl`, `StartupLifecycleHelper.FormatStartupBanner`, and `StartupLifecycleHelper.ShouldLaunchBrowser`.

- [ ] **Step 1: Write the failing unit tests**

```csharp
using System.Collections.Generic;
using AIAgentHub.Web.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace AgentHub.UnitTests.Web;

public class StartupLifecycleHelperTests
{
    [Theory]
    [InlineData("https://0.0.0.0:5432", "https://localhost:5432")]
    [InlineData("http://0.0.0.0:5433", "http://localhost:5433")]
    [InlineData("http://[::]:8080", "http://localhost:8080")]
    [InlineData("http://+:5000", "http://localhost:5000")]
    [InlineData("https://*:5001", "https://localhost:5001")]
    [InlineData("https://127.0.0.1:5432", "https://127.0.0.1:5432")]
    [InlineData("http://localhost:3000", "http://localhost:3000")]
    [InlineData("https://custom.domain:8443/hub", "https://custom.domain:8443/hub")]
    public void NormalizeUrl_ReplacesWildcardsWithLocalhost(string input, string expected)
    {
        var result = StartupLifecycleHelper.NormalizeUrl(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveListeningUrls_NormalizesAndDeduplicates()
    {
        var rawUrls = new[] { "https://0.0.0.0:5432", "http://0.0.0.0:5433", "https://localhost:5432" };
        var resolved = StartupLifecycleHelper.ResolveListeningUrls(rawUrls);

        Assert.Equal(2, resolved.Count);
        Assert.Contains("https://localhost:5432", resolved);
        Assert.Contains("http://localhost:5433", resolved);
    }

    [Fact]
    public void SelectPrimaryBrowserUrl_PrefersHttpsOverHttp()
    {
        var urls = new[] { "http://localhost:5433", "https://localhost:5432" };
        var primary = StartupLifecycleHelper.SelectPrimaryBrowserUrl(urls);

        Assert.Equal("https://localhost:5432", primary);
    }

    [Fact]
    public void SelectPrimaryBrowserUrl_ReturnsFirstHttpWhenNoHttps()
    {
        var urls = new[] { "http://localhost:5001", "http://localhost:5002" };
        var primary = StartupLifecycleHelper.SelectPrimaryBrowserUrl(urls);

        Assert.Equal("http://localhost:5001", primary);
    }

    [Fact]
    public void FormatStartupBanner_CreatesFormattedOutput()
    {
        var urls = new[] { "https://localhost:5432", "http://localhost:5433" };
        var banner = StartupLifecycleHelper.FormatStartupBanner(urls);

        Assert.Contains("AI Agent Hub is running!", banner);
        Assert.Contains("https://localhost:5432", banner);
        Assert.Contains("http://localhost:5433", banner);
    }

    [Fact]
    public void ShouldLaunchBrowser_ReturnsFalse_WhenTestingEnvironment()
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Testing");
        var config = new ConfigurationBuilder().Build();
        var args = Array.Empty<string>();

        var result = StartupLifecycleHelper.ShouldLaunchBrowser(args, config, env);

        Assert.False(result);
    }

    [Theory]
    [InlineData("--no-browser")]
    [InlineData("-no-browser")]
    [InlineData("/no-browser")]
    public void ShouldLaunchBrowser_ReturnsFalse_WhenCliFlagSupplied(string flag)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Production");
        var inMemory = new Dictionary<string, string?> { ["AgentHub:OpenBrowserAtStartup"] = "true" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var args = new[] { flag };

        var result = StartupLifecycleHelper.ShouldLaunchBrowser(args, config, env);

        Assert.False(result);
    }

    [Theory]
    [InlineData("--browser")]
    [InlineData("-browser")]
    [InlineData("/browser")]
    public void ShouldLaunchBrowser_ReturnsTrue_WhenCliBrowserFlagSupplied(string flag)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Production");
        var inMemory = new Dictionary<string, string?> { ["AgentHub:OpenBrowserAtStartup"] = "false" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var args = new[] { flag };

        var result = StartupLifecycleHelper.ShouldLaunchBrowser(args, config, env);

        Assert.True(result);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void ShouldLaunchBrowser_FallsBackToConfiguration_WhenNoCliFlag(string configValue, bool expected)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Production");
        var inMemory = new Dictionary<string, string?> { ["AgentHub:OpenBrowserAtStartup"] = configValue };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var args = Array.Empty<string>();

        var result = StartupLifecycleHelper.ShouldLaunchBrowser(args, config, env);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldLaunchBrowser_DefaultsToTrue_WhenNoCliAndNoConfig()
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Production");
        var config = new ConfigurationBuilder().Build();
        var args = Array.Empty<string>();

        var result = StartupLifecycleHelper.ShouldLaunchBrowser(args, config, env);

        Assert.True(result);
    }
}
```

- [ ] **Step 2: Run test to verify it fails to compile/run**

Run: `dotnet test tests/AgentHub.UnitTests/AgentHub.UnitTests.csproj --filter FullyQualifiedName~StartupLifecycleHelperTests`
Expected: FAIL / compilation error because `StartupLifecycleHelper` does not exist yet.

---

### Task 2: Implement `StartupLifecycleHelper`

**Files:**
- Create: `src/AIAgentHub.Web/Startup/StartupLifecycleHelper.cs`

**Interfaces:**
- Produces: `StartupLifecycleHelper` static class with:
  - `string NormalizeUrl(string rawUrl)`
  - `IReadOnlyList<string> ResolveListeningUrls(IEnumerable<string> rawUrls)`
  - `string? SelectPrimaryBrowserUrl(IEnumerable<string> urls)`
  - `string FormatStartupBanner(IEnumerable<string> urls)`
  - `bool ShouldLaunchBrowser(string[] args, IConfiguration configuration, IHostEnvironment environment)`
  - `void LaunchBrowser(string url)`
  - `void OnApplicationStarted(IServiceProvider services, string[] args, IConfiguration configuration, IHostEnvironment environment, TextWriter? consoleWriter = null)`

- [ ] **Step 1: Create `src/AIAgentHub.Web/Startup/StartupLifecycleHelper.cs`**

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace AIAgentHub.Web.Startup;

public static class StartupLifecycleHelper
{
    public static string NormalizeUrl(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return rawUrl;
        }

        var trimmed = rawUrl.Trim().TrimEnd('/');

        // Handle URI parsing with wildcard hosts
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            var host = uri.Host;
            if (host is "0.0.0.0" or "[::]" or "+" or "*" or "0")
            {
                var builder = new UriBuilder(uri)
                {
                    Host = "localhost"
                };
                return builder.Uri.ToString().TrimEnd('/');
            }
            return uri.ToString().TrimEnd('/');
        }

        // Fallback replacement for non-standard URI strings like http://+:5000 or https://*:5001
        var normalized = trimmed
            .Replace("://0.0.0.0:", "://localhost:")
            .Replace("://[::]:", "://localhost:")
            .Replace("://+:", "://localhost:")
            .Replace("://*:", "://localhost:");

        return normalized;
    }

    public static IReadOnlyList<string> ResolveListeningUrls(IEnumerable<string> rawUrls)
    {
        var resolved = new List<string>();
        foreach (var url in rawUrls)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var normalized = NormalizeUrl(url);
            if (!resolved.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                resolved.Add(normalized);
            }
        }

        return resolved;
    }

    public static string? SelectPrimaryBrowserUrl(IEnumerable<string> urls)
    {
        var list = urls.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        var httpsUrl = list.FirstOrDefault(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        return httpsUrl ?? list.FirstOrDefault(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) ?? list[0];
    }

    public static string FormatStartupBanner(IEnumerable<string> urls)
    {
        var urlList = urls.ToList();
        var sb = new StringBuilder();
        sb.AppendLine("==============================================================");
        sb.AppendLine("  AI Agent Hub is running!");
        
        var primary = SelectPrimaryBrowserUrl(urlList);
        if (primary != null)
        {
            sb.AppendLine($"  ➜ Local:    {primary}");
        }

        foreach (var url in urlList)
        {
            if (!string.Equals(url, primary, StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"  ➜ Fallback: {url}");
            }
        }

        sb.AppendLine("==============================================================");
        return sb.ToString();
    }

    public static bool ShouldLaunchBrowser(string[] args, IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return false;
        }

        var hasNoBrowserFlag = args.Any(a =>
            string.Equals(a, "--no-browser", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "-no-browser", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "/no-browser", StringComparison.OrdinalIgnoreCase));

        if (hasNoBrowserFlag)
        {
            return false;
        }

        var hasBrowserFlag = args.Any(a =>
            string.Equals(a, "--browser", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "-browser", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "/browser", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "--open-browser", StringComparison.OrdinalIgnoreCase));

        if (hasBrowserFlag)
        {
            return true;
        }

        var configValue = configuration.GetValue<bool?>("AgentHub:OpenBrowserAtStartup");
        if (configValue.HasValue)
        {
            return configValue.Value;
        }

        return true;
    }

    public static void LaunchBrowser(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _ = Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _ = Process.Start("open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _ = Process.Start("xdg-open", url);
            }
        }
        catch
        {
            // Non-fatal: Headless or environment without default browser association
        }
    }

    public static void OnApplicationStarted(
        IServiceProvider services,
        string[] args,
        IConfiguration configuration,
        IHostEnvironment environment,
        TextWriter? consoleWriter = null)
    {
        var writer = consoleWriter ?? Console.Out;

        var server = services.GetService(typeof(IServer)) as IServer;
        var addressFeature = server?.Features.Get<IServerAddressesFeature>();
        var boundAddresses = addressFeature?.Addresses;

        IEnumerable<string> rawUrls = boundAddresses != null && boundAddresses.Count > 0
            ? boundAddresses
            : (configuration["urls"]?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? ["https://0.0.0.0:5432", "http://0.0.0.0:5433"]);

        var normalizedUrls = ResolveListeningUrls(rawUrls);
        var banner = FormatStartupBanner(normalizedUrls);
        writer.WriteLine(banner);

        if (ShouldLaunchBrowser(args, configuration, environment))
        {
            var primaryUrl = SelectPrimaryBrowserUrl(normalizedUrls);
            if (!string.IsNullOrEmpty(primaryUrl))
            {
                LaunchBrowser(primaryUrl);
            }
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they pass**

Run: `dotnet test tests/AgentHub.UnitTests/AgentHub.UnitTests.csproj --filter FullyQualifiedName~StartupLifecycleHelperTests`
Expected: All 8+ unit tests PASS.

---

### Task 3: Integrate `StartupLifecycleHelper` into `Program.cs` and `appsettings.json`

**Files:**
- Modify: `src/AIAgentHub.Web/Program.cs`
- Modify: `src/AIAgentHub.Web/appsettings.json`
- Modify: `src/AIAgentHub.Web/appsettings.Development.json`

**Interfaces:**
- Consumes: `StartupLifecycleHelper.OnApplicationStarted`

- [ ] **Step 1: Update `appsettings.json` and `appsettings.Development.json`**

Add `"OpenBrowserAtStartup": true` under `"AgentHub"` object in both JSON configuration files.

- [ ] **Step 2: Update `Program.cs`**

Import `using AIAgentHub.Web.Startup;` and register the startup hook before `app.Run()`:
```csharp
app.Lifetime.ApplicationStarted.Register(() =>
{
    StartupLifecycleHelper.OnApplicationStarted(app.Services, args, builder.Configuration, app.Environment);
});
```

- [ ] **Step 3: Verify Solution Build & Test Suite**

Run:
```bash
dotnet build
dotnet test tests/AgentHub.UnitTests/AgentHub.UnitTests.csproj
dotnet test tests/AgentHub.IntegrationTests/AgentHub.IntegrationTests.csproj
```
Expected: All tests pass with zero failures and no unexpected browser launches.

---

### Task 4: Documentation Updates

**Files:**
- Modify: `README.md`
- Modify: `docs/technical/DevelopmentStandards.md`

- [ ] **Step 1: Update `README.md`**
Document the automatic browser launch, `--no-browser` CLI flag, and `AgentHub:OpenBrowserAtStartup` setting.

- [ ] **Step 2: Update `docs/technical/DevelopmentStandards.md`**
Document the startup lifecycle behavior, address normalization, and console banner printing.

---

### Task 5: Final Full-Suite Verification

- [ ] **Step 1: Execute all unit and integration tests**
Run: `npm run test:all`
Expected: All frontend tests, backend unit tests, and backend integration tests pass.
