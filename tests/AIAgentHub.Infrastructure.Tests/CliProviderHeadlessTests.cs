using System.Diagnostics;
using System.Text;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Providers;
using Microsoft.Extensions.Options;
using Xunit;

namespace AIAgentHub.Infrastructure.Tests;

public sealed class TestDotnetCliProvider : CliProviderBase
{
    public TestDotnetCliProvider(
        IOptions<CliExecutionOptions> options,
        IPromptLogger promptLogger,
        IProcessExecutor processExecutor)
        : base(options, promptLogger, processExecutor)
    {
    }

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

    public override string BuildArguments(ProviderExecutionContext context)
    {
        return "--list-sdks";
    }
}

public sealed class CliProviderHeadlessTests
{
    [Fact]
    public async Task ExecuteAsync_HeadlessEnabled_CapturesOutputAndStreamsTokens()
    {
        var options = Options.Create(new CliExecutionOptions { Headless = true, Shell = "PowerShell" });
        var loggerMock = NSubstitute.Substitute.For<IPromptLogger>();
        var headlessExecutor = new HeadlessProcessExecutor();
        var provider = new TestDotnetCliProvider(options, loggerMock, headlessExecutor);

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
        var loggerMock = NSubstitute.Substitute.For<IPromptLogger>();
        var headedExecutor = new HeadedProcessExecutor();
        var provider = new TestDotnetCliProvider(options, loggerMock, headedExecutor);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
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
            cts.Token
        );

        try
        {
            await provider.ExecuteAsync(context);
        }
        catch (OperationCanceledException)
        {
            // Expected cancellation timeout for interactive desktop process wait
        }

        var result = outputBuilder.ToString();
        Assert.NotEmpty(result);

        if (OperatingSystem.IsWindows())
        {
            Assert.Contains("[Test Dotnet CLI] External PowerShell session launched on desktop.", result);
        }
    }
}
