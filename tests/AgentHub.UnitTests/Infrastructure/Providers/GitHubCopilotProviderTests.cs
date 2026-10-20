using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Providers;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace AgentHub.UnitTests.Infrastructure.Providers;

public sealed class GitHubCopilotProviderTests
{
    private readonly IOptions<CliExecutionOptions> _options = Options.Create(new CliExecutionOptions { Headless = true });
    private readonly IPromptLogger _promptLogger = Substitute.For<IPromptLogger>();
    private readonly IProcessExecutor _executor = Substitute.For<IProcessExecutor>();

    private static async Task<(bool Success, string? ErrorMessage)> InvokeRunTestCommandAsync(GitHubCopilotProvider provider, string exePath)
    {
        var method = typeof(GitHubCopilotProvider).GetMethod(
            "RunTestCommandAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        var task = (Task)method.Invoke(provider, [exePath, CancellationToken.None])!;
        await task;
        var resultProp = task.GetType().GetProperty("Result");
        var result = resultProp?.GetValue(task);
        var successProp = result?.GetType().GetProperty("IsSuccess");
        var errorProp = result?.GetType().GetProperty("Error");
        var success = (bool)(successProp?.GetValue(result) ?? false);
        var error = (string?)errorProp?.GetValue(result);
        return (success, error);
    }

    private ProviderExecutionContext CreateContext(
        string? modelId,
        string prompt = "Implement feature X",
        string? sessionId = null,
        string? effort = null)
    {
        return new ProviderExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "C:\\test\\workspace",
            prompt,
            modelId,
            sessionId,
            [],
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult(true),
            CancellationToken.None,
            null,
            effort
        );
    }

    [Fact]
    public void GitHubCopilotProvider_InitialProperties_AreCorrect()
    {
        var provider = new GitHubCopilotProvider(_options, _promptLogger, _executor);

        Assert.Equal("copilot", provider.Id);
        Assert.Equal("copilot", provider.ExecutableName);
        Assert.Equal("npm install -g @github/copilot", provider.InstallCommand);
        Assert.Equal("GitHub Copilot", provider.DisplayName);
        Assert.Equal("login", provider.AuthCommand);
        Assert.True(provider.Capabilities.HasFlag(ProviderCapabilities.Streaming));
        Assert.True(provider.Capabilities.HasFlag(ProviderCapabilities.ToolCalling));
        Assert.True(provider.Capabilities.HasFlag(ProviderCapabilities.Skills));
        Assert.True(provider.Capabilities.HasFlag(ProviderCapabilities.Mcp));
        Assert.True(provider.Capabilities.HasFlag(ProviderCapabilities.FileEditing));
        Assert.True(provider.Capabilities.HasFlag(ProviderCapabilities.Vision));
        Assert.True(provider.Capabilities.HasFlag(ProviderCapabilities.ModelSelection));
    }

    [Fact]
    public void GitHubCopilotProvider_BuildArguments_InitialSession_UsesSessionIdFlag()
    {
        var provider = new GitHubCopilotProvider(_options, _promptLogger, _executor);
        var context = CreateContext(null, "Refactor service layer", sessionId: null);

        var args = provider.BuildArguments(context);

        Assert.Contains("--output-format text", args);
        Assert.Contains("--silent", args);
        Assert.Contains("--allow-all-tools", args);
        Assert.Contains("--add-dir \"C:\\test\\workspace\"", args);
        Assert.Contains($"--session-id \"{context.ConversationId}\"", args);
        Assert.Contains("-p \"Refactor service layer\"", args);
        Assert.DoesNotContain("--resume", args);
        Assert.DoesNotContain("--model", args);
    }

    [Fact]
    public void GitHubCopilotProvider_BuildArguments_ResumedSession_UsesResumeFlag()
    {
        var provider = new GitHubCopilotProvider(_options, _promptLogger, _executor);
        var sessionId = Guid.NewGuid().ToString();
        var context = CreateContext("claude-sonnet-4.6", "Fix unit tests", sessionId);

        var args = provider.BuildArguments(context);

        Assert.Contains("--output-format text", args);
        Assert.Contains("--silent", args);
        Assert.Contains("--allow-all-tools", args);
        Assert.Contains("--add-dir \"C:\\test\\workspace\"", args);
        Assert.Contains($"--resume \"{sessionId}\"", args);
        Assert.Contains("--model \"claude-sonnet-4.6\"", args);
        Assert.Contains("-p \"Fix unit tests\"", args);
        Assert.DoesNotContain("--session-id", args);
    }

    [Fact]
    public void GitHubCopilotProvider_BuildArguments_OmitsModel_WhenDefaultSpecified()
    {
        var provider = new GitHubCopilotProvider(_options, _promptLogger, _executor);
        var context = CreateContext("default", "Hello");

        var args = provider.BuildArguments(context);

        Assert.DoesNotContain("--model", args);
        Assert.Contains("-p \"Hello\"", args);
    }

    [Fact]
    public void GitHubCopilotProvider_ParseModelsHelpOutput_ParsesDynamicModelsFromHelpConfig()
    {
        var sampleHelpOutput = """
          `logLevel`: log level for CLI; defaults to "default". Set to "all" for debug logging.

          `model`: AI model to use for Copilot CLI; can be changed with /model command or --model flag option.
            - "claude-sonnet-5"
            - "claude-fable-5"
            - "claude-opus-5"
            - "gpt-5.6-terra"
            - "gpt-5.4"
            - "gemini-3.7-flash"
            - "grok-4.5"
            - "kimi-k3"

          `contextTier`: context window tier for tiered-pricing models.
        """;

        var models = GitHubCopilotProvider.ParseModelsHelpOutput(sampleHelpOutput);

        Assert.NotEmpty(models);
        Assert.Equal(9, models.Count); // default + 8 models

        // First model is default
        Assert.Equal("default", models[0].Id);
        Assert.True(models[0].IsDefault);
        Assert.True(models[0].IsDisplayed);

        // Dynamically parsed models
        Assert.Contains(models, m => m.Id == "claude-sonnet-5" && m.DisplayName == "Claude Sonnet 5");
        Assert.Contains(models, m => m.Id == "claude-fable-5");
        Assert.Contains(models, m => m.Id == "claude-opus-5");
        Assert.Contains(models, m => m.Id == "gpt-5.6-terra" && m.DisplayName == "Gpt 5.6 Terra");
        Assert.Contains(models, m => m.Id == "gpt-5.4");
        Assert.Contains(models, m => m.Id == "gemini-3.7-flash" && m.DisplayName == "Gemini 3.7 Flash");
        Assert.Contains(models, m => m.Id == "grok-4.5");
        Assert.Contains(models, m => m.Id == "kimi-k3");
    }

    [Fact]
    public void GitHubCopilotProvider_ParseModelsHelpOutput_EmptyOrMalformed_ReturnsEmpty()
    {
        Assert.Empty(GitHubCopilotProvider.ParseModelsHelpOutput(""));
        Assert.Empty(GitHubCopilotProvider.ParseModelsHelpOutput("Random output without model list"));
    }

    [Fact]
    public async Task GitHubCopilotProvider_ExecuteAsync_CapturesSessionIdOnFirstTurn()
    {
        var provider = new GitHubCopilotProvider(_options, _promptLogger, _executor);
        string? capturedSessionId = null;

        var context = new ProviderExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "C:\\test\\workspace",
            "Initial prompt",
            null,
            null,
            [],
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult(true),
            CancellationToken.None,
            sessionId =>
            {
                capturedSessionId = sessionId;
                return Task.CompletedTask;
            }
        );

        await provider.ExecuteAsync(context);

        Assert.Equal(context.ConversationId.ToString(), capturedSessionId);
    }

    [Theory]
    [InlineData("Please run the application in the background", true)]
    [InlineData("Start the web server and test endpoints", true)]
    [InlineData("Launch the calc project", true)]
    [InlineData("Listen on port 5050 and host the api", true)]
    [InlineData("Serve the client application", true)]
    [InlineData("Run the tests", true)]
    [InlineData("Refactor the authentication service", false)]
    [InlineData("Explain how this function works", false)]
    [InlineData("Fix the typo in README.md", false)]
    public void GitHubCopilotProvider_ShouldInjectDetachedExecutionInstruction_GatingWorks(string prompt, bool expected)
    {
        var triggerWords = new[] { "run", "start", "launch", "serve", "server", "host", "daemon", "listen", "up", "project", "calc", "web" };
        var actual = CliProviderBase.ShouldInjectDetachedExecutionInstruction(prompt, triggerWords);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GitHubCopilotProvider_ShouldInjectDetachedExecutionInstruction_DoesNotDuplicateIfAlreadyPresent()
    {
        var prompt = $"{CliProviderBase.DetachedExecutionInstruction} run the app";
        var triggerWords = new[] { "run" };
        var actual = CliProviderBase.ShouldInjectDetachedExecutionInstruction(prompt, triggerWords);
        Assert.False(actual);
    }

    [Fact]
    public void GitHubCopilotProvider_BuildArguments_WithTriggerWord_InjectsDetachedExecutionInstruction()
    {
        var options = Options.Create(new CliExecutionOptions
        {
            Headless = true,
            DetachedExecutionTriggerWords = ["run", "start"]
        });
        var provider = new GitHubCopilotProvider(options, _promptLogger, _executor);
        var context = CreateContext(null, "Please run the application in the background");

        var args = provider.BuildArguments(context);

        Assert.Contains(CliProviderBase.DetachedExecutionInstruction, args);
        Assert.Contains("Please run the application in the background", args);
    }

    [Fact]
    public void GitHubCopilotProvider_BuildArguments_WithoutTriggerWord_DoesNotInjectedInstruction()
    {
        var options = Options.Create(new CliExecutionOptions
        {
            Headless = true,
            DetachedExecutionTriggerWords = ["run", "start"]
        });
        var provider = new GitHubCopilotProvider(options, _promptLogger, _executor);
        var context = CreateContext(null, "Explain the structure of this code");

        var args = provider.BuildArguments(context);

        Assert.DoesNotContain(CliProviderBase.DetachedExecutionInstruction, args);
        Assert.Contains("-p \"Explain the structure of this code\"", args);
    }

    [Fact]
    public void GitHubCopilotProvider_GetDetachedExecutionTriggerWords_MergesGlobalAndProviderOverrides()
    {
        var globalOpts = Options.Create(new CliExecutionOptions
        {
            DetachedExecutionTriggerWords = ["run", "start"]
        });

        var providerSettings = new ProvidersOptions
        {
            ["copilot"] = new ProviderSettingsOptions
            {
                DetachedExecutionTriggerWords = ["customtrigger", "START"]
            }
        };
        var providersOpts = Options.Create(providerSettings);

        var provider = new GitHubCopilotProvider(globalOpts, _promptLogger, _executor, providersOpts);
        var words = provider.GetDetachedExecutionTriggerWords();

        Assert.Contains("run", words);
        Assert.Contains("start", words, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("customtrigger", words);
        Assert.Equal(3, words.Count); // "start" and "START" deduplicated
    }

    [Fact]
    public async Task TestAsync_WhenCopilotAuthenticated_ReturnsSuccess()
    {
        var executor = Substitute.For<IProcessExecutor>();
        executor.RunCommandAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>()
        ).Returns(Task.FromResult(new ProcessCommandResult(0, "pong", "")));

        var provider = new GitHubCopilotProvider(_options, _promptLogger, executor);
        var result = await InvokeRunTestCommandAsync(provider, "copilot");

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData("no authentication information found")]
    [InlineData("not logged in")]
    [InlineData("unauthenticated")]
    [InlineData("auth login required")]
    public async Task TestAsync_WhenCopilotNotAuthenticated_ReturnsFailure(string errorOutput)
    {
        var executor = Substitute.For<IProcessExecutor>();
        executor.RunCommandAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>()
        ).Returns(Task.FromResult(new ProcessCommandResult(1, "", errorOutput)));

        var provider = new GitHubCopilotProvider(_options, _promptLogger, executor);
        var result = await InvokeRunTestCommandAsync(provider, "copilot");

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task TestAsync_WhenCopilotThrows_ReturnsFailureWithExceptionMessage()
    {
        var executor = Substitute.For<IProcessExecutor>();
        executor.RunCommandAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>()
        ).Returns<Task<ProcessCommandResult>>(_ => throw new InvalidOperationException("Process crashed"));

        var provider = new GitHubCopilotProvider(_options, _promptLogger, executor);
        var result = await InvokeRunTestCommandAsync(provider, "copilot");

        Assert.False(result.Success);
        Assert.Contains("Process crashed", result.ErrorMessage);
    }
}
