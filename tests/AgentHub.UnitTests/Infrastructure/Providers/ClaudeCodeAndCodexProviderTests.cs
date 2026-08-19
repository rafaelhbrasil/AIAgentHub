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
    public async Task ClaudeCodeProvider_GetModelsAsync_ReturnsDefaultModel()
    {
        var provider = new ClaudeCodeProvider(_options, _promptLogger, _executor);

        var models = await provider.GetModelsAsync();

        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.Id == "default" && m.IsDefault);
        Assert.Single(models);
        Assert.Null(models[0].ContextWindow);
    }

    [Fact]
    public void ClaudeCodeProvider_BuildArguments_IncludesExpectedFlags_WithResumeWhenSessionIdProvided()
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
        Assert.Contains($"--resume \"{sessionId}\"", args);
    }

    [Fact]
    public void ClaudeCodeProvider_BuildArguments_IncludesSessionIdFlag_WhenInitialSession()
    {
        var provider = new ClaudeCodeProvider(_options, _promptLogger, _executor);
        var context = CreateContext(null, "Fix bug in parser", sessionId: null);

        var args = provider.BuildArguments(context);

        Assert.Contains($"-p \"Fix bug in parser\"", args);
        Assert.Contains($"--session-id \"{context.ConversationId}\"", args);
        Assert.DoesNotContain("--resume", args);
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
    public async Task CodexCliProvider_GetModelsAsync_ReturnsDefaultModel()
    {
        var provider = new CodexCliProvider(_options, _promptLogger, _executor);

        var models = await provider.GetModelsAsync();

        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.Id == "default" && m.IsDefault);
        Assert.Single(models);
        Assert.Null(models[0].ContextWindow);
    }

    [Fact]
    public void CodexCliProvider_BuildArguments_IncludesExpectedFlags_WithResumeWhenSessionIdProvided()
    {
        var provider = new CodexCliProvider(_options, _promptLogger, _executor);
        var sessionId = Guid.NewGuid().ToString();
        var context = CreateContext("o3-mini", "Optimize DB query", sessionId, "high");

        var args = provider.BuildArguments(context);

        Assert.StartsWith("exec resume", args);
        Assert.Contains("--dangerously-bypass-approvals-and-sandbox", args);
        Assert.Contains("--skip-git-repo-check", args);
        Assert.Contains("--model \"o3-mini\"", args);
        Assert.Contains("-c model_reasoning_effort=high", args);
        Assert.Contains(sessionId, args);
        Assert.Contains("\"Optimize DB query\"", args);
    }

    [Fact]
    public void CodexCliProvider_BuildArguments_InitialSession_UsesExecWithoutResume()
    {
        var provider = new CodexCliProvider(_options, _promptLogger, _executor);
        var context = CreateContext("o3-mini", "Optimize DB query", sessionId: null);

        var args = provider.BuildArguments(context);

        Assert.StartsWith("exec --dangerously-bypass-approvals-and-sandbox", args);
        Assert.DoesNotContain("resume", args);
        Assert.Contains("--model \"o3-mini\"", args);
        Assert.Contains("\"Optimize DB query\"", args);
    }

    [Fact]
    public void CodexCliProvider_ParseModelsJson_ParsesModelsCorrectly()
    {
        var sampleJson = """
        {
          "models": [
            {
              "slug": "gpt-5.6-terra",
              "display_name": "GPT-5.6-Terra",
              "description": "Balanced agentic coding model for everyday work.",
              "visibility": "list",
              "context_window": 272000
            },
            {
              "slug": "o3-mini",
              "display_name": "o3-mini",
              "description": "Fast reasoning model.",
              "visibility": "list",
              "context_window": 200000
            },
            {
              "slug": "internal-model",
              "display_name": "Internal Test",
              "description": "Hidden test model.",
              "visibility": "hidden",
              "context_window": 128000
            }
          ]
        }
        """;

        var models = CodexCliProvider.ParseModelsJson(sampleJson);

        Assert.Equal(3, models.Count);

        var first = models[0];
        Assert.Equal("gpt-5.6-terra", first.Id);
        Assert.Equal("GPT-5.6-Terra", first.DisplayName);
        Assert.Equal("Balanced agentic coding model for everyday work.", first.Description);
        Assert.Equal(272000, first.ContextWindow);
        Assert.True(first.IsDefault);
        Assert.True(first.IsDisplayed);

        var second = models[1];
        Assert.Equal("o3-mini", second.Id);
        Assert.False(second.IsDefault);
        Assert.True(second.IsDisplayed);

        var third = models[2];
        Assert.Equal("internal-model", third.Id);
        Assert.False(third.IsDefault);
        Assert.False(third.IsDisplayed);
    }

    [Fact]
    public void CodexCliProvider_BuildArguments_OmitsModel_WhenDefaultModelSpecified()
    {
        var provider = new CodexCliProvider(_options, _promptLogger, _executor);
        var context = CreateContext("default", "Optimize DB query");

        var args = provider.BuildArguments(context);

        Assert.DoesNotContain("--model", args);
        Assert.Contains("\"Optimize DB query\"", args);
    }

    [Fact]
    public void CodexCliProvider_ParseModelsJson_InvalidOrEmptyOutput_ReturnsEmpty()
    {
        Assert.Empty(CodexCliProvider.ParseModelsJson(""));
        Assert.Empty(CodexCliProvider.ParseModelsJson("Not a json"));
        Assert.Empty(CodexCliProvider.ParseModelsJson("{}"));
    }
}
