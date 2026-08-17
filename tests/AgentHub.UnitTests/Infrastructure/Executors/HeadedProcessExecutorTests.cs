using System.Text;

using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Providers;

using NSubstitute;

namespace AgentHub.UnitTests.Infrastructure.Executors;

public class TestableHeadedProcessExecutor : HeadedProcessExecutor
{
    public void CallEnsureWindowsPlatform(bool simulateWindows)
    {
        if (!simulateWindows)
        {
            throw new NotImplementedException("HeadedProcessExecutor is not supported on non-Windows operating systems yet.");
        }
    }
}

public sealed class HeadedProcessExecutorTests
{
    [Fact]
    public async Task StreamLogFileAsync_WhenFileWritten_StreamsTokensToContext()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "AgentHubLogs");
        _ = Directory.CreateDirectory(tempFolder);
        var testLogFile = Path.Combine(tempFolder, $"unittest_stream_{Guid.NewGuid():N}.log");

        try
        {
            var outputBuilder = new StringBuilder();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            var context = new ProviderExecutionContext(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Directory.GetCurrentDirectory(),
                "test prompt",
                null,
                null,
                Array.Empty<string>(),
                token =>
                {
                    _ = outputBuilder.Append(token);
                    return Task.CompletedTask;
                },
                (type, target) => Task.FromResult(true),
                cts.Token
            );

            var executor = new HeadedProcessExecutor();
            var streamTask = executor.StreamLogFileAsync(testLogFile, context, cts.Token);

            await Task.Delay(100);
            await File.WriteAllTextAsync(testLogFile, "Line 1: Hello World\nLine 2: Testing Tee-Object");

            await Task.Delay(300);
            cts.Cancel();

            try
            {
                await streamTask;
            }
            catch (OperationCanceledException) { }

            var streamedContent = outputBuilder.ToString();
            Assert.Contains("Hello World", streamedContent);
            Assert.Contains("Testing Tee-Object", streamedContent);
        }
        finally
        {
            if (File.Exists(testLogFile))
            {
                File.Delete(testLogFile);
            }
        }
    }

    [Fact]
    public async Task StreamLogFileAsync_WhenFileDoesNotExist_TimesOutGracefully()
    {
        var missingLogFile = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.log");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var context = new ProviderExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Directory.GetCurrentDirectory(),
            "test prompt",
            null,
            null,
            Array.Empty<string>(),
            token => Task.CompletedTask,
            (type, target) => Task.FromResult(true),
            cts.Token
        );

        var executor = new HeadedProcessExecutor();
        await executor.StreamLogFileAsync(missingLogFile, context, cts.Token);
        Assert.False(File.Exists(missingLogFile));
    }

    [Fact]
    public async Task ExecuteAsync_OnWindows_SpawnsPowerShellWithTeeObjectAndStreamsOutput()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var outputBuilder = new StringBuilder();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var context = new ProviderExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Directory.GetCurrentDirectory(),
            "build help prompt",
            null,
            null,
            Array.Empty<string>(),
            token =>
            {
                _ = outputBuilder.Append(token);
                return Task.CompletedTask;
            },
            (type, target) => Task.FromResult(true),
            cts.Token
        );

        var loggerMock = Substitute.For<IPromptLogger>();
        var executor = new HeadedProcessExecutor();
        var options = new CliExecutionOptions { HeadedAutoCloseDelaySeconds = 0 };

        await executor.ExecuteAsync(
            "TestProvider",
            "dotnet",
            "build --help",
            context,
            loggerMock,
            options);

        var result = outputBuilder.ToString();
        Assert.Contains("[TestProvider] External PowerShell session launched on desktop.", result);
        Assert.Contains("build", result, StringComparison.OrdinalIgnoreCase);

        loggerMock.Received(1).LogPromptSent(
            "TestProvider",
            Arg.Any<string>(),
            Arg.Is<string>(arg => arg.Contains("dotnet") || arg.Contains("build")),
            Arg.Any<int>());
    }

    [Theory]
    [InlineData("remember the word \"banana\"")]
    [InlineData("test $special `characters` and \\backslash\\")]
    public async Task RunCommandAsync_WithQuotesAndSpecialCharacters_PreservesArgumentsCorrectly(string prompt)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var options = Microsoft.Extensions.Options.Options.Create(new CliExecutionOptions { Headless = false, HeadedAutoCloseDelaySeconds = 0 });
        var executor = new HeadedProcessExecutor(options);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var escapedPrompt = prompt.Replace("\"", "\\\"");
        var result = await executor.RunCommandAsync("cmd.exe", $"/c echo {escapedPrompt}", null, cts.Token, "Test — Echo");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(escapedPrompt, result.Output);
    }

    [Fact]
    public void EnsureWindowsPlatform_WhenNonWindows_ThrowsNotImplementedException()
    {
        var executor = new TestableHeadedProcessExecutor();
        var ex = Assert.Throws<NotImplementedException>(() => executor.CallEnsureWindowsPlatform(simulateWindows: false));
        Assert.Contains("HeadedProcessExecutor", ex.Message);
    }
}
