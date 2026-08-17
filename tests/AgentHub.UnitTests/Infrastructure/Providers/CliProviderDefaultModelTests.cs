using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Providers;

using Microsoft.Extensions.Options;

namespace AgentHub.UnitTests.Infrastructure.Providers;

public sealed class CliProviderDefaultModelTests
{
    private readonly IOptions<CliExecutionOptions> _options = Options.Create(new CliExecutionOptions { Headless = true });
    private readonly IPromptLogger _promptLogger = NSubstitute.Substitute.For<IPromptLogger>();
    private readonly IProcessExecutor _executor = NSubstitute.Substitute.For<IProcessExecutor>();

    private ProviderExecutionContext CreateContext(string? modelId, string prompt = "Write a test")
    {
        return new ProviderExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "C:\\test\\workspace",
            prompt,
            modelId,
            null,
            Array.Empty<string>(),
            _ => Task.CompletedTask,
            (_, _) => Task.FromResult(true),
            CancellationToken.None
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("default")]
    [InlineData("DEFAULT")]
    [InlineData("Default")]
    public void IsDefaultModel_IdentifiesDefaultVariants(string? modelId)
    {
        Assert.True(CliProviderBase.IsDefaultModel(modelId));
    }

    [Theory]
    [InlineData("gemini-3.7-flash")]
    [InlineData("claude-3-7-sonnet")]
    [InlineData("o3-mini")]
    [InlineData("deepseek-r1")]
    public void IsDefaultModel_ReturnsFalseForSpecificModels(string modelId)
    {
        Assert.False(CliProviderBase.IsDefaultModel(modelId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("default")]
    public void AntigravityProvider_BuildArguments_OmitsModelFlag_WhenDefault(string? modelId)
    {
        var provider = new AntigravityProvider(_options, _promptLogger, _executor);
        var context = CreateContext(modelId);

        var args = provider.BuildArguments(context);

        Assert.DoesNotContain("--model", args);
    }

    [Fact]
    public void AntigravityProvider_BuildArguments_IncludesModelFlag_WhenExplicitModelProvided()
    {
        var provider = new AntigravityProvider(_options, _promptLogger, _executor);
        var context = CreateContext("gemini-3.7-flash");

        var args = provider.BuildArguments(context);

        Assert.Contains("--model \"gemini-3.7-flash\"", args);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("default")]
    public void ClaudeCodeProvider_BuildArguments_OmitsModelFlag_WhenDefault(string? modelId)
    {
        var provider = new ClaudeCodeProvider(_options, _promptLogger, _executor);
        var context = CreateContext(modelId);

        var args = provider.BuildArguments(context);

        Assert.DoesNotContain("--model", args);
    }

    [Fact]
    public void ClaudeCodeProvider_BuildArguments_IncludesModelFlag_WhenExplicitModelProvided()
    {
        var provider = new ClaudeCodeProvider(_options, _promptLogger, _executor);
        var context = CreateContext("claude-3-7-sonnet");

        var args = provider.BuildArguments(context);

        Assert.Contains("--model \"claude-3-7-sonnet\"", args);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("default")]
    public void CodexCliProvider_BuildArguments_OmitsModelFlag_WhenDefault(string? modelId)
    {
        var provider = new CodexCliProvider(_options, _promptLogger, _executor);
        var context = CreateContext(modelId);

        var args = provider.BuildArguments(context);

        Assert.DoesNotContain("--model", args);
    }

    [Fact]
    public void CodexCliProvider_BuildArguments_IncludesModelFlag_WhenExplicitModelProvided()
    {
        var provider = new CodexCliProvider(_options, _promptLogger, _executor);
        var context = CreateContext("o3-mini");

        var args = provider.BuildArguments(context);

        Assert.Contains("--model \"o3-mini\"", args);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("default")]
    public void OpenCodeProvider_BuildArguments_OmitsModelFlag_WhenDefault(string? modelId)
    {
        var provider = new OpenCodeProvider(_options, _promptLogger, _executor);
        var context = CreateContext(modelId);

        var args = provider.BuildArguments(context);

        Assert.DoesNotContain("--model", args);
    }

    [Fact]
    public void OpenCodeProvider_BuildArguments_IncludesModelFlag_WhenExplicitModelProvided()
    {
        var provider = new OpenCodeProvider(_options, _promptLogger, _executor);
        var context = CreateContext("deepseek-r1");

        var args = provider.BuildArguments(context);

        Assert.Contains("--model \"deepseek-r1\"", args);
    }
}
