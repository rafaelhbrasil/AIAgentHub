using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Providers;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace AgentHub.UnitTests.Infrastructure.Providers;

public class FastPreCheckTests
{
    [Fact]
    public void IsInstalledFastCheck_WhenExecutableExists_ReturnsTrue()
    {
        // Arrange
        var mockExecutor = Substitute.For<IProcessExecutor>();
        var mockLogger = Substitute.For<IPromptLogger>();
        var options = Options.Create(new CliExecutionOptions());

        var provider = new TestableCliProvider(options, mockLogger, mockExecutor, "cmd");

        // Act
        var result = provider.IsInstalledFastCheck();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsInstalledFastCheck_WhenExecutableDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var mockExecutor = Substitute.For<IProcessExecutor>();
        var mockLogger = Substitute.For<IPromptLogger>();
        var options = Options.Create(new CliExecutionOptions());

        var provider = new TestableCliProvider(options, mockLogger, mockExecutor, "non_existent_binary_xyz_9999");

        // Act
        var result = provider.IsInstalledFastCheck();

        // Assert
        Assert.False(result);
    }

    private class TestableCliProvider(
        IOptions<CliExecutionOptions> options,
        IPromptLogger logger,
        IProcessExecutor executor,
        string exeName) : CliProviderBase(options, logger, executor)
    {
        public override string Id => "test";
        public override string ExecutableName => exeName;
        public override string? InstallCommand => null;
        public override ProviderCapability Capabilities => ProviderCapability.None;
    }
}
