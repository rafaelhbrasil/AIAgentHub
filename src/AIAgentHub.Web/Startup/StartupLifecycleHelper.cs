using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

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

        // Fallback for non-standard URI strings like http://+:5000 or https://*:5001
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
            // Non-fatal fallback for environments without GUI or browser association
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
