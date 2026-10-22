using System.Text;

using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Providers;

using Microsoft.Extensions.Options;
using NSubstitute;

namespace AgentHub.UnitTests.Infrastructure.Providers;

public sealed class TestDotnetCliProvider(
    IOptions<CliExecutionOptions> options,
    IPromptLogger promptLogger,
    IProcessExecutor processExecutor) : CliProviderBase(options, promptLogger, processExecutor)
{
    public override string Id => "test-dotnet";
    public override string DisplayName => "Test Dotnet CLI";
    public override string Description => "Test CLI Provider for Integration Testing";
    public override string ExecutableName => "dotnet";
    public override string? InstallInstructions => null;
    public override string? InstallCommand => null;
    public override string? AuthCommand => null;
    public override string? DocumentationUrl => null;
    public override ProviderCapabilities Capabilities => ProviderCapabilities.Streaming;

    public override Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ModelInfo>>([]);

    public override string BuildArguments(ProviderExecutionContext context) => "--list-sdks";

    public bool TestIsQuotaError(string? error) => IsQuotaError(error);
    public bool TestIsAuthError(string? error) => IsAuthError(error);
    public DateTimeOffset? TestParseQuotaResetTime(string error) => ParseQuotaResetTime(error);
    public string TestBaseBuildArguments(ProviderExecutionContext ctx) => base.BuildArguments(ctx);
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
            [],
            token =>
            {
                _ = outputBuilder.Append(token);
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
        var headedExecutor = new HeadedProcessExecutor(options);
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
            [],
            token =>
            {
                _ = outputBuilder.Append(token);
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

    [Fact]
    public async Task RunCommandAsync_HeadlessExecutor_ExecutesAndReturnsOutput()
    {
        var executor = new HeadlessProcessExecutor();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var result = await executor.RunCommandAsync("dotnet", "--version", null, cts.Token, "Test — Version");

        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(result.Output.Trim());
    }

    [Fact]
    public async Task RunCommandAsync_HeadedExecutor_ExecutesScriptAndReturnsOutput()
    {
        var options = Options.Create(new CliExecutionOptions { Headless = false, HeadedAutoCloseDelaySeconds = 0 });
        var executor = new HeadedProcessExecutor(options);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var result = await executor.RunCommandAsync("dotnet", "--version", null, cts.Token, "Test — Version");

        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(result.Output.Trim());
    }

    [Fact]
    public void FindExecutable_ResolvesCommonSystemAndToolExecutables()
    {
        var dotnetExe = CliProviderBase.FindExecutable("dotnet");
        Assert.NotNull(dotnetExe);
        Assert.True(File.Exists(dotnetExe));

        var missing = CliProviderBase.FindExecutable("non_existent_binary_12345");
        Assert.Null(missing);
    }

    [Fact]
    public void CliProviderBase_IsQuotaError_And_IsAuthError_IdentifyPatternsCorrectly()
    {
        var options = Options.Create(new CliExecutionOptions { Headless = true });
        var loggerMock = NSubstitute.Substitute.For<IPromptLogger>();
        var executorMock = NSubstitute.Substitute.For<IProcessExecutor>();
        var provider = new TestDotnetCliProvider(options, loggerMock, executorMock);

        // Quota
        Assert.False(provider.TestIsQuotaError(null));
        Assert.False(provider.TestIsQuotaError(""));
        Assert.False(provider.TestIsQuotaError("All normal here"));
        Assert.True(provider.TestIsQuotaError("Error: API quota exceeded"));
        Assert.True(provider.TestIsQuotaError("Rate limit reached"));
        Assert.True(provider.TestIsQuotaError("HTTP 429 Too Many Requests"));

        // Auth
        Assert.False(provider.TestIsAuthError(null));
        Assert.False(provider.TestIsAuthError(""));
        Assert.False(provider.TestIsAuthError("All normal"));
        Assert.True(provider.TestIsAuthError("Auth failed: please login"));
        Assert.True(provider.TestIsAuthError("HTTP 401 Unauthorized"));
        Assert.True(provider.TestIsAuthError("Invalid credential provided"));
    }

    [Fact]
    public void CliProviderBase_ParseQuotaResetTime_ParsesRelativeAndIsoTimes()
    {
        var options = Options.Create(new CliExecutionOptions { Headless = true });
        var loggerMock = NSubstitute.Substitute.For<IPromptLogger>();
        var executorMock = NSubstitute.Substitute.For<IProcessExecutor>();
        var provider = new TestDotnetCliProvider(options, loggerMock, executorMock);

        var resetHours = provider.TestParseQuotaResetTime("Rate limit exceeded. Resets in 2 hours");
        Assert.NotNull(resetHours);
        Assert.True(resetHours.Value > DateTimeOffset.UtcNow.AddMinutes(90));

        var resetMinutes = provider.TestParseQuotaResetTime("Quota exceeded. Reset in 15 minutes");
        Assert.NotNull(resetMinutes);

        var resetDays = provider.TestParseQuotaResetTime("Daily limit reached. Resets in 1 days");
        Assert.NotNull(resetDays);

        var resetIso = provider.TestParseQuotaResetTime("Try again after 2026-10-21T12:00:00Z");
        Assert.NotNull(resetIso);
        Assert.Equal(2026, resetIso.Value.Year);

        var resetNull = provider.TestParseQuotaResetTime("Some random error text");
        Assert.Null(resetNull);
    }

    [Fact]
    public async Task CliProviderBase_AbortAsync_And_EndSessionAsync_DelegateToExecutor()
    {
        var options = Options.Create(new CliExecutionOptions { Headless = true });
        var loggerMock = NSubstitute.Substitute.For<IPromptLogger>();
        var executorMock = NSubstitute.Substitute.For<IProcessExecutor>();
        var convId = Guid.NewGuid();
        executorMock.AbortProcess(convId).Returns(true);

        var provider = new TestDotnetCliProvider(options, loggerMock, executorMock);
        await provider.AbortAsync(convId);
        await provider.EndSessionAsync(convId);

        executorMock.Received(2).AbortProcess(convId);
    }

    [Fact]
    public void CliProviderBase_BaseBuildArguments_FormatsPromptModelAndSession()
    {
        var options = Options.Create(new CliExecutionOptions { Headless = true });
        var loggerMock = NSubstitute.Substitute.For<IPromptLogger>();
        var executorMock = NSubstitute.Substitute.For<IProcessExecutor>();
        var provider = new TestDotnetCliProvider(options, loggerMock, executorMock);

        var context = new ProviderExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "C:\\workspace",
            "Prompt with \"test\"",
            "custom-model",
            "session-xyz",
            [],
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult(true),
            CancellationToken.None
        );

        var args = provider.TestBaseBuildArguments(context);
        Assert.Contains("--prompt \"Prompt with \\\"test\\\"\"", args);
        Assert.Contains("--model \"custom-model\"", args);
        Assert.Contains("--session \"session-xyz\"", args);
    }
}
