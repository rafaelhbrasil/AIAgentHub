using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Providers;

using Microsoft.Extensions.Options;

using NSubstitute;

namespace AgentHub.UnitTests.Infrastructure.Providers;

public sealed class ClaudeCodeAndCodexProviderTests
{
    private readonly IOptions<CliExecutionOptions> _options = Options.Create(new CliExecutionOptions { Headless = true });
    private readonly IPromptLogger _promptLogger = Substitute.For<IPromptLogger>();
    private readonly IProcessExecutor _executor = Substitute.For<IProcessExecutor>();

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
            Array.Empty<string>(),
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult(true),
            CancellationToken.None,
            null,
            effort
        );
    }

    [Fact]
    public async Task ClaudeCodeProvider_GetModelsAsync_ReturnsCuratedModels_WithSonnet37Default()
    {
        var provider = new ClaudeCodeProvider(_options, _promptLogger, _executor);

        var models = await provider.GetModelsAsync();

        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.Id == "claude-3-7-sonnet" && m.IsDefault);
        Assert.Contains(models, m => m.Id == "claude-3-5-sonnet");
        Assert.Contains(models, m => m.Id == "claude-3-5-haiku");
        Assert.Contains(models, m => m.Id == "claude-3-opus");
    }

    [Fact]
    public void ClaudeCodeProvider_BuildArguments_IncludesExpectedFlags()
    {
        var provider = new ClaudeCodeProvider(_options, _promptLogger, _executor);
        var sessionId = Guid.NewGuid().ToString();
        var context = CreateContext("claude-3-7-sonnet", "Fix bug in parser", sessionId, "high");

        var args = provider.BuildArguments(context);

        Assert.Contains("-p \"Fix bug in parser\"", args);
        Assert.Contains("--output-format text", args);
        Assert.Contains("--permission-mode acceptEdits", args);
        Assert.Contains("--model \"claude-3-7-sonnet\"", args);
        Assert.Contains("--effort \"high\"", args);
        Assert.Contains($"--session-id \"{sessionId}\"", args);
    }

    [Fact]
    public void ClaudeCodeProvider_BuildArguments_OmitsModel_WhenDefaultModelSpecified()
    {
        var provider = new ClaudeCodeProvider(_options, _promptLogger, _executor);
        var context = CreateContext("default", "Fix bug in parser");

        var args = provider.BuildArguments(context);

        Assert.DoesNotContain("--model", args);
        Assert.Contains("-p \"Fix bug in parser\"", args);
    }

    [Fact]
    public async Task CodexCliProvider_GetModelsAsync_ReturnsCuratedFallbackModels_WithO3MiniDefault()
    {
        var provider = new CodexCliProvider(_options, _promptLogger, _executor);

        var models = await provider.GetModelsAsync();

        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.Id == "o3-mini" && m.IsDefault);
        Assert.Contains(models, m => m.Id == "o1");
        Assert.Contains(models, m => m.Id == "gpt-4o");
        Assert.Contains(models, m => m.Id == "gpt-4o-mini");
    }

    [Fact]
    public void CodexCliProvider_BuildArguments_IncludesExpectedFlags()
    {
        var provider = new CodexCliProvider(_options, _promptLogger, _executor);
        var sessionId = Guid.NewGuid().ToString();
        var context = CreateContext("o3-mini", "Optimize DB query", sessionId);

        var args = provider.BuildArguments(context);

        Assert.Contains("--prompt \"Optimize DB query\"", args);
        Assert.Contains("--model \"o3-mini\"", args);
        Assert.Contains($"--session \"{sessionId}\"", args);
    }

    [Fact]
    public void CodexCliProvider_BuildArguments_OmitsModel_WhenDefaultModelSpecified()
    {
        var provider = new CodexCliProvider(_options, _promptLogger, _executor);
        var context = CreateContext("default", "Optimize DB query");

        var args = provider.BuildArguments(context);

        Assert.DoesNotContain("--model", args);
        Assert.Contains("--prompt \"Optimize DB query\"", args);
    }
}
