using System.Diagnostics;
using System.Text;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Providers;
using Microsoft.Extensions.Options;
using Xunit;

namespace AIAgentHub.Infrastructure.Tests;

public sealed class TestDotnetCliProvider : CliProviderBase
{
    public TestDotnetCliProvider(IOptions<CliExecutionOptions>? options = null) : base(options) { }

    public override string Id => "test-dotnet";
    public override string DisplayName => "Test Dotnet CLI";
    public override string Description => "Test CLI Provider for Integration Testing";
    public override string ExecutableName => "dotnet";
    public override string? InstallInstructions => null;
    public override string? InstallCommand => null;
    public override string? AuthCommand => null;
    public override string? DocumentationUrl => null;
    public override ProviderCapability Capabilities => ProviderCapability.Streaming;

    public override Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ModelInfo>>(Array.Empty<ModelInfo>());
    }

    protected override void ConfigureStartInfo(ProcessStartInfo psi, ProviderExecutionContext context)
    {
        psi.Arguments = "--list-sdks";
    }

    public override string FormatArgumentsForShell(string exePath, ProviderExecutionContext context)
    {
        return $"& '{exePath}' --list-sdks";
    }
}

public sealed class CliProviderHeadlessTests
{
    [Fact]
    public async Task ExecuteAsync_HeadlessEnabled_CapturesOutputAndStreamsTokens()
    {
        var options = Options.Create(new CliExecutionOptions { Headless = true, Shell = "PowerShell" });
        var provider = new TestDotnetCliProvider(options);

        var outputBuilder = new StringBuilder();
        var context = new ProviderExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Directory.GetCurrentDirectory(),
            "list sdks",
            null,
            null,
            Array.Empty<string>(),
            token =>
            {
                outputBuilder.Append(token);
                return Task.CompletedTask;
            },
            (type, target) => Task.FromResult(true),
            CancellationToken.None
        );

        await provider.ExecuteAsync(context);

        var result = outputBuilder.ToString();
        Assert.NotEmpty(result);
        Assert.Contains(".", result);

        if (OperatingSystem.IsWindows())
        {
            Assert.Contains("[C:\\Program Files\\dotnet\\sdk]", result);
        }
    }

    [Fact]
    public async Task ExecuteAsync_HeadlessDisabled_LaunchesDesktopPowerShellMessageAndCapturesOutput()
    {
        var options = Options.Create(new CliExecutionOptions { Headless = false, Shell = "PowerShell" });
        var provider = new TestDotnetCliProvider(options);

        var outputBuilder = new StringBuilder();
        var context = new ProviderExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Directory.GetCurrentDirectory(),
            "list sdks",
            null,
            null,
            Array.Empty<string>(),
            token =>
            {
                outputBuilder.Append(token);
                return Task.CompletedTask;
            },
            (type, target) => Task.FromResult(true),
            CancellationToken.None
        );

        await provider.ExecuteAsync(context);

        var result = outputBuilder.ToString();
        Assert.NotEmpty(result);
        Assert.Contains(".", result);

        if (OperatingSystem.IsWindows())
        {
            Assert.Contains("[Test Dotnet CLI] External PowerShell session launched on desktop.", result);
            Assert.Contains("[C:\\Program Files\\dotnet\\sdk]", result);
        }

    }
}
