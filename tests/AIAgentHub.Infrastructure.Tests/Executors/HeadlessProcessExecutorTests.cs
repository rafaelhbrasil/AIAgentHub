using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Providers;
using NSubstitute;
using Xunit;

namespace AIAgentHub.Infrastructure.Tests.Executors;

public class TestableHeadlessProcessExecutor : HeadlessProcessExecutor
{
    public void CallEnsureWindowsPlatform(bool simulateWindows)
    {
        if (!simulateWindows)
        {
            throw new NotImplementedException("HeadlessProcessExecutor is not supported on non-Windows operating systems yet.");
        }
    }

    public static string TestCleanAnsiCodes(string input) => CleanAnsiCodes(input);
    public static bool TestShouldFilterErrorChunk(string chunk) => ShouldFilterErrorChunk(chunk);
}

public sealed class HeadlessProcessExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_OnWindows_ExecutesProcessAndStreamsOutput()
    {
        if (!OperatingSystem.IsWindows()) return;

        var executor = new HeadlessProcessExecutor();
        var loggerMock = Substitute.For<IPromptLogger>();
        var activeProcesses = new ConcurrentDictionary<Guid, Process>();
        var outputBuilder = new StringBuilder();

        var context = new ProviderExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Directory.GetCurrentDirectory(),
            "list sdks prompt",
            null,
            null,
            Array.Empty<string>(),
            token =>
            {
                outputBuilder.Append(token);
                return Task.CompletedTask;
            },
            (type, target) => Task.FromResult(true),
            CancellationToken.None
        );

        var options = new CliExecutionOptions { Headless = true };

        await executor.ExecuteAsync(
            "TestDotnet",
            "dotnet",
            "--list-sdks",
            context,
            loggerMock,
            options);

        var result = outputBuilder.ToString();
        Assert.NotEmpty(result);
        Assert.Contains("[C:\\Program Files\\dotnet\\sdk]", result);
        Assert.False(executor.AbortProcess(context.ConversationId));

        loggerMock.Received(1).LogPromptSent(
            "TestDotnet",
            "default",
            Arg.Is<string>(s => s.Contains("--list-sdks")),
            Arg.Any<int>());
    }

    [Fact]
    public void EnsureWindowsPlatform_WhenNonWindows_ThrowsNotImplementedException()
    {
        var executor = new TestableHeadlessProcessExecutor();
        var ex = Assert.Throws<NotImplementedException>(() => executor.CallEnsureWindowsPlatform(simulateWindows: false));
        Assert.Contains("HeadlessProcessExecutor", ex.Message);
    }

    [Theory]
    [InlineData("\x1B[31mError Message\x1B[0m", "Error Message")]
    [InlineData("Clean Text", "Clean Text")]
    public void CleanAnsiCodes_RemovesEscapeSequences(string input, string expected)
    {
        var result = TestableHeadlessProcessExecutor.TestCleanAnsiCodes(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("> prompt header", true)]
    [InlineData("compiling build · project", true)]
    [InlineData("Fatal compile error", false)]
    public void ShouldFilterErrorChunk_FiltersNoise(string chunk, bool expectedFilter)
    {
        var result = TestableHeadlessProcessExecutor.TestShouldFilterErrorChunk(chunk);
        Assert.Equal(expectedFilter, result);
    }
}
