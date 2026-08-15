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
}
