using AIAgentHub.Infrastructure.Logging;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentHub.UnitTests.Infrastructure.Logging;

public sealed class FileLoggerProviderTests
{
    [Fact]
    public void FileLogger_WhenLogCalled_WritesFormattedMessageToFile()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"AgentHubFileLogTests_{Guid.NewGuid():N}");
        var pathPattern = Path.Combine(tempDir, "test-{Date}.log");
        using var provider = new FileLoggerProvider(pathPattern);
        var logger = provider.CreateLogger("TestCategory");

        try
        {
            // Act
            logger.LogInformation("Hello file logging world! ConvId: {ConvId}", 12345);

            // Assert
            var dateStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var expectedFile = Path.Combine(tempDir, $"test-{dateStr}.log");
            Assert.True(File.Exists(expectedFile));

            var content = File.ReadAllText(expectedFile);
            Assert.Contains("[Information]", content);
            Assert.Contains("[TestCategory]", content);
            Assert.Contains("Hello file logging world! ConvId: 12345", content);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void FileLogger_WhenExceptionProvided_AppendsExceptionDetails()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"AgentHubFileLogTests_{Guid.NewGuid():N}");
        var pathPattern = Path.Combine(tempDir, "test-{Date}.log");
        using var provider = new FileLoggerProvider(pathPattern);
        var logger = provider.CreateLogger("TestCategory");

        try
        {
            // Act
            try
            {
                throw new InvalidOperationException("Simulated error occurred");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure in operation");
            }

            // Assert
            var dateStr = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var expectedFile = Path.Combine(tempDir, $"test-{dateStr}.log");
            Assert.True(File.Exists(expectedFile));

            var content = File.ReadAllText(expectedFile);
            Assert.Contains("[Error]", content);
            Assert.Contains("Failure in operation", content);
            Assert.Contains("InvalidOperationException: Simulated error occurred", content);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void AddFile_WhenDisabledInConfiguration_DoesNotAddProvider()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Logging:File:Enabled"] = "false",
            ["Logging:File:Path"] = "logs/test-{Date}.log"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

        var services = new ServiceCollection();
        _ = services.AddLogging(loggingBuilder =>
        {
            _ = loggingBuilder.AddFile(configuration);
        });

        // Act
        using var sp = services.BuildServiceProvider();
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var loggerProviders = sp.GetServices<ILoggerProvider>();

        // Assert
        Assert.DoesNotContain(loggerProviders, p => p is FileLoggerProvider);
    }
}
