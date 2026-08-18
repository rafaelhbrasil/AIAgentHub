using System.Text.RegularExpressions;

using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Providers;

using Microsoft.Extensions.Options;

using NSubstitute;

namespace AgentHub.UnitTests.Infrastructure.Providers;

public sealed class CliProviderSessionTests
{
    private readonly IOptions<CliExecutionOptions> _options = Options.Create(new CliExecutionOptions { Headless = true });
    private readonly IPromptLogger _promptLogger = Substitute.For<IPromptLogger>();
    private readonly IProcessExecutor _executor = Substitute.For<IProcessExecutor>();

    [Fact]
    public async Task AntigravityProvider_ExecuteAsync_WhenSessionIdNull_ExtractsConversationGuidFromLogAndCallsOnSessionCreated()
    {
        var provider = new AntigravityProvider(_options, _promptLogger, _executor);
        var expectedSessionId = "4b192c05-c428-4d44-9520-fd7afd520a9f";
        string? capturedSessionId = null;

        _executor.When(x => x.ExecuteAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ProviderExecutionContext>(),
            Arg.Any<IPromptLogger>(),
            Arg.Any<CliExecutionOptions>()
        )).Do(callInfo =>
        {
            var args = callInfo.ArgAt<string>(2);
            var logMatch = Regex.Match(args, @"--log-file\s+""([^""]+)""");
            if (logMatch.Success)
            {
                var logPath = logMatch.Groups[1].Value;
                var logDir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                {
                    _ = Directory.CreateDirectory(logDir);
                }
                File.WriteAllText(logPath, $"I0816 12:17:25 server.go:1074] Created conversation {expectedSessionId}\nPrint mode: conversation={expectedSessionId}");
            }
        });

        var context = new ProviderExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "C:\\test\\workspace",
            "what's the ID of the current conversation?",
            null,
            null,
            Array.Empty<string>(),
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult(true),
            CancellationToken.None,
            newSessionId =>
            {
                capturedSessionId = newSessionId;
                return Task.CompletedTask;
            }
        );

        await provider.ExecuteAsync(context);

        Assert.Equal(expectedSessionId, capturedSessionId);
    }

    [Fact]
    public void AntigravityProvider_BuildArguments_IncludesConversationFlag_WhenSessionIdProvided()
    {
        var provider = new AntigravityProvider(_options, _promptLogger, _executor);
        var sessionId = "4b192c05-c428-4d44-9520-fd7afd520a9f";

        var context = new ProviderExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "C:\\test\\workspace",
            "what's the ID of the current conversation?",
            null,
            sessionId,
            Array.Empty<string>(),
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult(true),
            CancellationToken.None
        );

        var args = provider.BuildArguments(context);

        Assert.Contains($"--conversation \"{sessionId}\"", args);
    }

    [Fact]
    public async Task ClaudeCodeProvider_StartSessionAsync_ReturnsNull()
    {
        var provider = new ClaudeCodeProvider(_options, _promptLogger, _executor);
        var conversationId = Guid.NewGuid();

        var sessionId = await provider.StartSessionAsync(conversationId, "C:\\test\\workspace", null);

        Assert.Null(sessionId);
    }

    [Fact]
    public async Task ClaudeCodeProvider_ExecuteAsync_WhenSessionIdNull_CallsOnSessionCreatedWithConversationId()
    {
        var provider = new ClaudeCodeProvider(_options, _promptLogger, _executor);
        var conversationId = Guid.NewGuid();
        string? capturedSessionId = null;

        var context = new ProviderExecutionContext(
            conversationId,
            Guid.NewGuid(),
            "C:\\test\\workspace",
            "test prompt",
            null,
            null,
            Array.Empty<string>(),
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult(true),
            CancellationToken.None,
            newSessionId =>
            {
                capturedSessionId = newSessionId;
                return Task.CompletedTask;
            }
        );

        await provider.ExecuteAsync(context);

        Assert.Equal(conversationId.ToString(), capturedSessionId);
    }

    [Fact]
    public void ClaudeCodeProvider_BuildArguments_IncludesResumeFlag_WhenSessionIdProvided()
    {
        var provider = new ClaudeCodeProvider(_options, _promptLogger, _executor);
        var sessionId = Guid.NewGuid().ToString();

        var context = new ProviderExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "C:\\test\\workspace",
            "test prompt",
            null,
            sessionId,
            Array.Empty<string>(),
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult(true),
            CancellationToken.None
        );

        var args = provider.BuildArguments(context);

        Assert.Contains($"--resume \"{sessionId}\"", args);
    }

    [Fact]
    public void ClaudeCodeProvider_BuildArguments_IncludesSessionIdFlag_WhenSessionIdNull()
    {
        var provider = new ClaudeCodeProvider(_options, _promptLogger, _executor);
        var conversationId = Guid.NewGuid();

        var context = new ProviderExecutionContext(
            conversationId,
            Guid.NewGuid(),
            "C:\\test\\workspace",
            "test prompt",
            null,
            null,
            Array.Empty<string>(),
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult(true),
            CancellationToken.None
        );

        var args = provider.BuildArguments(context);

        Assert.Contains($"--session-id \"{conversationId}\"", args);
    }

    [Fact]
    public async Task CodexCliProvider_StartSessionAsync_ReturnsNull()
    {
        var provider = new CodexCliProvider(_options, _promptLogger, _executor);
        var conversationId = Guid.NewGuid();

        var sessionId = await provider.StartSessionAsync(conversationId, "C:\\test\\workspace", null);

        Assert.Null(sessionId);
    }

    [Fact]
    public void CodexCliProvider_BuildArguments_IncludesResume_WhenSessionIdProvided()
    {
        var provider = new CodexCliProvider(_options, _promptLogger, _executor);
        var sessionId = Guid.NewGuid().ToString();

        var context = new ProviderExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "C:\\test\\workspace",
            "test prompt",
            null,
            sessionId,
            Array.Empty<string>(),
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult(true),
            CancellationToken.None
        );

        var args = provider.BuildArguments(context);

        Assert.StartsWith("exec resume", args);
        Assert.Contains(sessionId, args);
    }

    [Fact]
    public void CodexCliProvider_BuildArguments_ExecutesNewSession_WhenSessionIdNull()
    {
        var provider = new CodexCliProvider(_options, _promptLogger, _executor);

        var context = new ProviderExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "C:\\test\\workspace",
            "test prompt",
            null,
            null,
            Array.Empty<string>(),
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult(true),
            CancellationToken.None
        );

        var args = provider.BuildArguments(context);

        Assert.StartsWith("exec --dangerously-bypass-approvals-and-sandbox", args);
        Assert.DoesNotContain("resume", args);
    }
}
