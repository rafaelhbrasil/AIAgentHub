using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Providers;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace AgentHub.UnitTests.Infrastructure.Providers;

public sealed class CliProviderParsingAndDetectionTests
{
    private sealed class TestCliProvider(
        IOptions<CliExecutionOptions> options,
        IPromptLogger promptLogger,
        IProcessExecutor processExecutor,
        IOptions<ProvidersOptions>? providersOptions = null)
        : CliProviderBase(options, promptLogger, processExecutor, providersOptions)
    {
        public override string Id => "test-cli";
        public override string ExecutableName => "testcli";
        public override string? InstallCommand => "npm i -g testcli";
        public override ProviderCapabilities Capabilities => ProviderCapabilities.Streaming;

        protected override string DefaultDisplayName => "Test CLI";
        protected override string DefaultDescription => "Test Description";
        protected override string? DefaultInstallInstructions => "Install Test CLI instructions";
        protected override string? DefaultAuthCommand => "testcli auth";
        protected override string? DefaultDocumentationUrl => "https://example.com/testcli";

        public string? PublicFindExecutable(string name) => FindExecutable(name);
        public bool PublicIsQuotaError(string? error) => IsQuotaError(error);
        public bool PublicIsAuthError(string? error) => IsAuthError(error);
        public DateTimeOffset? PublicParseQuotaResetTime(string error) => ParseQuotaResetTime(error);
        public Task<IReadOnlyList<ModelInfo>> PublicTryFetchDynamicModelsAsync(string args, CancellationToken ct) =>
            TryFetchDynamicModelsAsync(args, ct);
    }

    private readonly IOptions<CliExecutionOptions> _options = Options.Create(new CliExecutionOptions { Headless = true });
    private readonly IPromptLogger _promptLogger = Substitute.For<IPromptLogger>();
    private readonly IProcessExecutor _executor = Substitute.For<IProcessExecutor>();

    [Fact]
    public void DefaultProperties_And_Helpers_ReturnExpectedValues()
    {
        var provider = new TestCliProvider(_options, _promptLogger, _executor);

        Assert.Equal("Test CLI", provider.DisplayName);
        Assert.Equal("Test Description", provider.Description);
        Assert.Equal("Install Test CLI instructions", provider.InstallInstructions);
        Assert.Equal("testcli auth", provider.AuthCommand);
        Assert.Equal("https://example.com/testcli", provider.DocumentationUrl);

        var notFound = provider.PublicFindExecutable("non_existent_binary_xyz_9999");
        Assert.Null(notFound);
    }

    [Theory]
    [InlineData("rate limit exceeded", true)]
    [InlineData("Too many requests (429)", true)]
    [InlineData("Quota exhausted", true)]
    [InlineData("Normal error", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsQuotaError_DetectsQuotaStrings(string? error, bool expected)
    {
        var provider = new TestCliProvider(_options, _promptLogger, _executor);
        Assert.Equal(expected, provider.PublicIsQuotaError(error));
    }

    [Theory]
    [InlineData("401 Unauthorized", true)]
    [InlineData("Login required", true)]
    [InlineData("Invalid credentials", true)]
    [InlineData("Authentication failure", true)]
    [InlineData("Syntax error", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAuthError_DetectsAuthStrings(string? error, bool expected)
    {
        var provider = new TestCliProvider(_options, _promptLogger, _executor);
        Assert.Equal(expected, provider.PublicIsAuthError(error));
    }

    [Fact]
    public void ParseQuotaResetTime_ParsesHoursMinutesDaysAndIso()
    {
        var provider = new TestCliProvider(_options, _promptLogger, _executor);

        var hourResult = provider.PublicParseQuotaResetTime("Rate limit exceeded. Resets in 2 hours");
        Assert.NotNull(hourResult);
        Assert.True(hourResult.Value > DateTimeOffset.UtcNow.AddMinutes(90));

        var minuteResult = provider.PublicParseQuotaResetTime("Quota reached. Reset in 45 minutes");
        Assert.NotNull(minuteResult);
        Assert.True(minuteResult.Value > DateTimeOffset.UtcNow.AddMinutes(40));

        var dayResult = provider.PublicParseQuotaResetTime("Too many requests. Resets in 1 days");
        Assert.NotNull(dayResult);
        Assert.True(dayResult.Value > DateTimeOffset.UtcNow.AddHours(20));

        var isoResult = provider.PublicParseQuotaResetTime("Try again after 2026-10-25T14:30:00Z");
        Assert.NotNull(isoResult);
        Assert.Equal(2026, isoResult.Value.Year);
        Assert.Equal(10, isoResult.Value.Month);
        Assert.Equal(25, isoResult.Value.Day);

        var noneResult = provider.PublicParseQuotaResetTime("Generic error without any time information");
        Assert.Null(noneResult);
    }

    [Fact]
    public async Task DetectDetailedAsync_WhenNotInstalled_ReturnsNotInstalledStatus()
    {
        var provider = new TestCliProvider(_options, _promptLogger, _executor);
        var result = await provider.DetectDetailedAsync();

        Assert.Equal(ProviderStatus.NotInstalled, result.Status);
        Assert.Contains("Test CLI is not installed", result.Message);
    }

    [Fact]
    public async Task OpenCodeProvider_GetLatestSessionIdAsync_WorkspaceAndOverallFallbacks()
    {
        var executor = Substitute.For<IProcessExecutor>();
        var workspace = "C:\\test\\workspace";

        // JSON where title doesn't match agenthub-convId, but directory matches workspace
        var jsonOutput = """
        [
            { "id": "ses_first111", "title": "unrelated", "directory": "C:\\other\\dir" },
            { "id": "ses_matchDir222", "title": "different", "directory": "C:\\test\\workspace" }
        ]
        """;

        executor.RunCommandAsync(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("session list")),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string>()
        ).Returns(Task.FromResult(new ProcessCommandResult(0, jsonOutput, "")));

        string? capturedSession = null;
        var context = new ProviderExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            workspace,
            "prompt",
            null,
            null,
            [],
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult(true),
            CancellationToken.None,
            OnSessionCreated: s => { capturedSession = s; return Task.CompletedTask; },
            null
        );

        var provider = new OpenCodeProvider(_options, _promptLogger, executor);
        await provider.ExecuteAsync(context);

        Assert.Equal("ses_matchDir222", capturedSession);
    }

    [Fact]
    public async Task OpenCodeProvider_GetLatestSessionIdAsync_WithHeaderFooterNoise()
    {
        var executor = Substitute.For<IProcessExecutor>();
        var convId = Guid.NewGuid();
        var workspace = "C:\\test\\workspace";

        // Wrapped in runner header and footer
        var outputWithNoise = $$"""
        [Headed Process Runner: opencode]
        Some debug initialization logs
        [
            { "id": "ses_noise999", "title": "agenthub-{{convId}}", "directory": "C:\\test\\workspace" }
        ]
        Process terminated cleanly with exit code 0.
        """;

        executor.RunCommandAsync(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("session list")),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string>()
        ).Returns(Task.FromResult(new ProcessCommandResult(0, outputWithNoise, "")));

        string? capturedSession = null;
        var context = new ProviderExecutionContext(
            convId,
            Guid.NewGuid(),
            workspace,
            "prompt",
            null,
            null,
            [],
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult(true),
            CancellationToken.None,
            OnSessionCreated: s => { capturedSession = s; return Task.CompletedTask; },
            null
        );

        var provider = new OpenCodeProvider(_options, _promptLogger, executor);
        await provider.ExecuteAsync(context);

        Assert.Equal("ses_noise999", capturedSession);
    }

    [Fact]
    public async Task OpenCodeProvider_GetModelsAsync_ParsesModelsCleanly()
    {
        var executor = Substitute.For<IProcessExecutor>();
        var modelsOutput = """
        === Available Models ===
        Usage: opencode [options]
        openai/gpt-4o	OpenAI GPT-4o
        anthropic/claude-3-5-sonnet    Claude 3.5 Sonnet
        ollama/llama3
        """;

        executor.RunCommandAsync(
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("models")),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string>()
        ).Returns(Task.FromResult(new ProcessCommandResult(0, modelsOutput, "")));

        var provider = new OpenCodeProvider(_options, _promptLogger, executor);
        var models = await provider.GetModelsAsync();

        Assert.NotNull(models);
        Assert.NotEmpty(models);
    }
}
