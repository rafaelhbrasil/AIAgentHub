using System;
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

    [Fact]
    public void OnApplicationStarted_WritesBannerToConsoleWriter()
    {
        var services = Substitute.For<IServiceProvider>();
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Testing");
        var inMemory = new Dictionary<string, string?> { ["urls"] = "https://0.0.0.0:5432;http://0.0.0.0:5433" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var args = Array.Empty<string>();

        using var stringWriter = new System.IO.StringWriter();
        StartupLifecycleHelper.OnApplicationStarted(services, args, config, env, stringWriter);

        var output = stringWriter.ToString();
        Assert.Contains("AI Agent Hub is running!", output);
        Assert.Contains("https://localhost:5432", output);
        Assert.Contains("http://localhost:5433", output);
    }
}
