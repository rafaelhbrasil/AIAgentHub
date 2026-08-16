using AIAgentHub.Infrastructure.Providers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using NSubstitute;

namespace AIAgentHub.Infrastructure.Tests;

public sealed class PromptLoggerTests
{
    private readonly ILogger<PromptLogger> _loggerMock;
    private readonly IConfiguration _configMock;

    public PromptLoggerTests()
    {
        _loggerMock = Substitute.For<ILogger<PromptLogger>>();
        _configMock = Substitute.For<IConfiguration>();
    }

    [Fact]
    public void IsEnabled_WhenConfigTrue_ReturnsTrue()
    {
        // Arrange
        _ = _configMock["AgentHub:PromptLogging:Enabled"].Returns("true");
        var logger = new PromptLogger(_loggerMock, _configMock);

        // Act & Assert
        Assert.True(logger.IsEnabled);
    }

    [Fact]
    public void IsEnabled_WhenConfigFalse_ReturnsFalse()
    {
        // Arrange
        _ = _configMock["AgentHub:PromptLogging:Enabled"].Returns("false");
        var logger = new PromptLogger(_loggerMock, _configMock);

        // Act & Assert
        Assert.False(logger.IsEnabled);
    }

    [Fact]
    public void IsEnabled_WhenConfigMissing_ReturnsTrue()
    {
        // Arrange - config value not set, should default to true
        _ = _configMock["AgentHub:PromptLogging:Enabled"].Returns((string?)null);
        var logger = new PromptLogger(_loggerMock, _configMock);

        // Act & Assert
        Assert.True(logger.IsEnabled);
    }

    [Fact]
    public void LogPromptSent_WhenDisabled_DoesNotLog()
    {
        // Arrange
        _ = _configMock["AgentHub:PromptLogging:Enabled"].Returns("false");
        var logger = new PromptLogger(_loggerMock, _configMock);

        // Act
        logger.LogPromptSent("TestProvider", "test-model", "test command", 100);

        // Assert
        _loggerMock.DidNotReceive().Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogPromptSent_WhenEnabled_LogsRedactedCommand()
    {
        // Arrange
        _ = _configMock["AgentHub:PromptLogging:Enabled"].Returns("true");
        var logger = new PromptLogger(_loggerMock, _configMock);

        // Act
        logger.LogPromptSent("TestProvider", "test-model", "test --prompt 'hello world'", 11);

        // Assert
        _loggerMock.Received(1).Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogPromptSent_WithEmptyCommandLine_LogsSuccessfully()
    {
        // Arrange
        _ = _configMock["AgentHub:PromptLogging:Enabled"].Returns("true");
        var logger = new PromptLogger(_loggerMock, _configMock);

        // Act
        logger.LogPromptSent("TestProvider", "test-model", "", 0);

        // Assert
        _loggerMock.Received(1).Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogPromptSent_WithNullCommandLine_LogsSuccessfully()
    {
        // Arrange
        _ = _configMock["AgentHub:PromptLogging:Enabled"].Returns("true");
        var logger = new PromptLogger(_loggerMock, _configMock);

        // Act
        logger.LogPromptSent("TestProvider", "test-model", null!, 0);

        // Assert
        _loggerMock.Received(1).Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogPromptSent_WithSpecialCharacters_LogsSuccessfully()
    {
        // Arrange
        _ = _configMock["AgentHub:PromptLogging:Enabled"].Returns("true");
        var logger = new PromptLogger(_loggerMock, _configMock);

        // Act
        logger.LogPromptSent("TestProvider", "test-model", "test --prompt 'hello \"world\" with $pecial chars!'", 42);

        // Assert
        _loggerMock.Received(1).Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogCommandResult_WhenSuccess_LogsDebug()
    {
        // Arrange
        _ = _configMock["AgentHub:PromptLogging:Enabled"].Returns("true");
        var logger = new PromptLogger(_loggerMock, _configMock);

        // Act
        logger.LogCommandResult("Antigravity CLI", "List Models", "agy models", 0, "model-1\nmodel-2", null);

        // Assert
        _loggerMock.Received(1).Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogCommandResult_WhenFailed_LogsError()
    {
        // Arrange
        _ = _configMock["AgentHub:PromptLogging:Enabled"].Returns("true");
        var logger = new PromptLogger(_loggerMock, _configMock);

        // Act
        logger.LogCommandResult("OpenCode", "Session List", "opencode session list", 1, null, "fatal: unexpected failure");

        // Assert
        _loggerMock.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogCommandResult_WhenAuthFailed_LogsWarning()
    {
        // Arrange
        _ = _configMock["AgentHub:PromptLogging:Enabled"].Returns("true");
        var logger = new PromptLogger(_loggerMock, _configMock);

        // Act
        logger.LogCommandResult("Claude Code", "Auth Status", "claude auth status", 1, "not logged in", "Authentication required");

        // Assert
        _loggerMock.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogCommandResult_WhenQuotaExceeded_LogsWarning()
    {
        // Arrange
        _ = _configMock["AgentHub:PromptLogging:Enabled"].Returns("true");
        var logger = new PromptLogger(_loggerMock, _configMock);

        // Act
        logger.LogCommandResult("Claude Code", "Execution", "claude --prompt test", 1, null, "Rate limit / quota exceeded (429)");

        // Assert
        _loggerMock.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogProviderStatus_WhenReady_LogsInformation()
    {
        // Arrange
        _ = _configMock["AgentHub:PromptLogging:Enabled"].Returns("true");
        var logger = new PromptLogger(_loggerMock, _configMock);

        // Act
        logger.LogProviderStatus("Antigravity CLI", AIAgentHub.Domain.Providers.ProviderStatus.Ready, "Operational");

        // Assert
        _loggerMock.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogProviderStatus_WhenUnauthenticated_LogsWarning()
    {
        // Arrange
        _ = _configMock["AgentHub:PromptLogging:Enabled"].Returns("true");
        var logger = new PromptLogger(_loggerMock, _configMock);

        // Act
        logger.LogProviderStatus("Claude Code", AIAgentHub.Domain.Providers.ProviderStatus.Unauthenticated, "Please login");

        // Assert
        _loggerMock.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void LogProviderStatus_WhenError_LogsError()
    {
        // Arrange
        _ = _configMock["AgentHub:PromptLogging:Enabled"].Returns("true");
        var logger = new PromptLogger(_loggerMock, _configMock);

        // Act
        logger.LogProviderStatus("Gemini CLI", AIAgentHub.Domain.Providers.ProviderStatus.Error, "Discontinued");

        // Assert
        _loggerMock.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
