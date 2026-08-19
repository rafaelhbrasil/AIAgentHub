using System.Text;
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
    public void ClaudeCodeProvider_ParseModelsOutput_ParsesAvailableAndCurrentModelCorrectly()
    {
        var sampleOutput = """
        "minimax-m3:cloud" is not a model this version of Claude Code recognizes, so auto-compact will keep this session within 200k tokens (the context window it assumes). If the model accepts more, append [1m] to the model name for 1M, or set CLAUDE_CODE_MAX_CONTEXT_TOKENS to its real window; to make it recognized, map it in the modelOverrides setting or update Claude Code; CLAUDE_CODE_DISABLE_UNKNOWN_MODEL_WINDOW_ENFORCEMENT=1 restores the previous wait-for-the-API behavior.
        Current model: minimax-m3:cloud
        Usage: /model <name>. Available: sonnet, opus, haiku, fable, best, sonnet[1m], opus[1m], fable[1m], opusplan, default, or a full model ID.
        """;

        var models = ClaudeCodeProvider.ParseModelsOutput(sampleOutput);

        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.Id == "minimax-m3:cloud" && m.IsDefault);
        Assert.Contains(models, m => m.Id == "sonnet");
        Assert.Contains(models, m => m.Id == "opus");
        Assert.Contains(models, m => m.Id == "haiku");
        Assert.Contains(models, m => m.Id == "fable");
        Assert.Contains(models, m => m.Id == "best");
        Assert.Contains(models, m => m.Id == "sonnet[1m]" && m.DisplayName == "Sonnet [1M]");
        Assert.Contains(models, m => m.Id == "opus[1m]" && m.DisplayName == "Opus [1M]");
        Assert.Contains(models, m => m.Id == "fable[1m]" && m.DisplayName == "Fable [1M]");
        Assert.Contains(models, m => m.Id == "opusplan");
        Assert.Contains(models, m => m.Id == "default");
        Assert.DoesNotContain(models, m => m.Id.Contains("full model ID", StringComparison.OrdinalIgnoreCase));
        Assert.All(models, m => Assert.Null(m.ContextWindow));
    }

    [Fact]
    public void ClaudeCodeProvider_ParseModelsOutput_EmptyOrInvalid_ReturnsEmpty()
    {
        Assert.Empty(ClaudeCodeProvider.ParseModelsOutput(""));
        Assert.Empty(ClaudeCodeProvider.ParseModelsOutput("Random warning without pattern"));
    }

    [Fact]
    public void ClaudeCodeProvider_ParseEffortOutput_ParsesEffortsCorrectly_FilteringUltracode()
    {
        var sampleOutput = """
        "minimax-m3:cloud" is not a model this version of Claude Code recognizes...
        Usage: /effort <low|medium|high|xhigh|max|ultracode|auto>
        """;

        var efforts = ClaudeCodeProvider.ParseEffortOutput(sampleOutput);

        Assert.Equal(6, efforts.Count);
        Assert.Contains("low", efforts);
        Assert.Contains("medium", efforts);
        Assert.Contains("high", efforts);
        Assert.Contains("xhigh", efforts);
        Assert.Contains("max", efforts);
        Assert.Contains("auto", efforts);
        Assert.DoesNotContain("ultracode", efforts);
        Assert.DoesNotContain("ultrathink", efforts);
    }

    [Fact]
    public void ClaudeCodeProvider_ParseEffortOutput_EmptyOrInvalid_ReturnsEmpty()
    {
        Assert.Empty(ClaudeCodeProvider.ParseEffortOutput(""));
        Assert.Empty(ClaudeCodeProvider.ParseEffortOutput("Usage: /effort without braces"));
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

    [Fact]
    public async Task CodexCliProvider_ProcessBufferAsync_ExtractsSessionIdAndStreamsMessage()
    {
        var streamedTokens = new List<string>();
        string? capturedSessionId = null;

        var context = new ProviderExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "C:\\test\\ws",
            "hello",
            null,
            null,
            Array.Empty<string>(),
            token =>
            {
                streamedTokens.Add(token);
                return Task.CompletedTask;
            },
            (_, _) => Task.FromResult(true),
            CancellationToken.None,
            sessionId =>
            {
                capturedSessionId = sessionId;
                return Task.CompletedTask;
            }
        );

        var buffer = new StringBuilder();
        var isFirstMessage = true;

        // Chunk 1: thread.started and turn.started
        _ = buffer.Append("Reading prompt from stdin...\n{\"type\":\"thread.started\",\"thread_id\":\"01a01ab1-test-session-id\"}\n{\"type\":\"turn.started\"}\n");
        await CodexCliProvider.ProcessBufferAsync(buffer, context, isFirst => isFirstMessage = isFirst, isFirstMessage, isFinal: false);

        Assert.Equal("01a01ab1-test-session-id", capturedSessionId);
        Assert.Empty(streamedTokens);

        // Chunk 2: item.completed with agent message and turn.completed
        _ = buffer.Append("{\"type\":\"item.completed\",\"item\":{\"id\":\"item_0\",\"type\":\"agent_message\",\"text\":\"Hello! How can I help?\"}}\n{\"type\":\"turn.completed\",\"usage\":{\"input_tokens\":100,\"output_tokens\":10}}\n");
        await CodexCliProvider.ProcessBufferAsync(buffer, context, isFirst => isFirstMessage = isFirst, isFirstMessage, isFinal: false);

        Assert.Single(streamedTokens);
        Assert.Equal("Hello! How can I help?", streamedTokens[0]);
    }

    [Fact]
    public async Task CodexCliProvider_ProcessBufferAsync_HandlesErrorEvent()
    {
        var streamedTokens = new List<string>();

        var context = new ProviderExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "C:\\test\\ws",
            "hello",
            null,
            null,
            Array.Empty<string>(),
            token =>
            {
                streamedTokens.Add(token);
                return Task.CompletedTask;
            },
            (_, _) => Task.FromResult(true),
            CancellationToken.None
        );

        var buffer = new StringBuilder();
        var isFirstMessage = true;

        _ = buffer.Append("{\"type\":\"error\",\"message\":\"{\\\"error\\\":{\\\"message\\\":\\\"Model not supported.\\\"}}\"}\n");
        await CodexCliProvider.ProcessBufferAsync(buffer, context, isFirst => isFirstMessage = isFirst, isFirstMessage, isFinal: false);

        Assert.Single(streamedTokens);
        Assert.Contains("Model not supported.", streamedTokens[0]);
    }

    [Fact]
    public async Task CodexCliProvider_ProcessBufferAsync_StreamsCommandExecutionProgress()
    {
        var streamedTokens = new List<string>();

        var context = new ProviderExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "C:\\test\\ws",
            "hello",
            null,
            null,
            Array.Empty<string>(),
            token =>
            {
                streamedTokens.Add(token);
                return Task.CompletedTask;
            },
            (_, _) => Task.FromResult(true),
            CancellationToken.None
        );

        var buffer = new StringBuilder();
        var isFirstMessage = true;

        // Command started
        _ = buffer.Append("{\"type\":\"item.started\",\"item\":{\"id\":\"item_1\",\"type\":\"command_execution\",\"command\":\"dotnet build\"}}\n");
        await CodexCliProvider.ProcessBufferAsync(buffer, context, isFirst => isFirstMessage = isFirst, isFirstMessage, isFinal: false);

        Assert.Single(streamedTokens);
        Assert.Contains("Running command:", streamedTokens[0]);
        Assert.Contains("dotnet build", streamedTokens[0]);

        // Command completed
        _ = buffer.Append("{\"type\":\"item.completed\",\"item\":{\"id\":\"item_1\",\"type\":\"command_execution\",\"command\":\"dotnet build\",\"aggregated_output\":\"Build succeeded.\"}}\n");
        await CodexCliProvider.ProcessBufferAsync(buffer, context, isFirst => isFirstMessage = isFirst, isFirstMessage, isFinal: false);

        Assert.Equal(2, streamedTokens.Count);
        Assert.Contains("Build succeeded.", streamedTokens[1]);
    }
}
