using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Providers;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace AgentHub.UnitTests.Infrastructure.Providers;

public sealed class OpenCodeProviderTests
{
    private readonly IOptions<CliExecutionOptions> _options = Options.Create(new CliExecutionOptions { Headless = true });
    private readonly IPromptLogger _promptLogger = Substitute.For<IPromptLogger>();
    private readonly IProcessExecutor _executor = Substitute.For<IProcessExecutor>();

    private static ProviderExecutionContext CreateContext(
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
    public void OpenCodeProvider_InitialProperties_AreCorrect()
    {
        var provider = new OpenCodeProvider(_options, _promptLogger, _executor);

        Assert.Equal("opencode", provider.Id);
        Assert.Equal("opencode", provider.ExecutableName);
        Assert.Equal("OpenCode", provider.DisplayName);
        Assert.True(provider.Capabilities.HasFlag(ProviderCapabilities.Streaming));
        Assert.True(provider.Capabilities.HasFlag(ProviderCapabilities.ToolCalling));
        Assert.True(provider.Capabilities.HasFlag(ProviderCapabilities.Mcp));
        Assert.True(provider.Capabilities.HasFlag(ProviderCapabilities.FileEditing));
        Assert.True(provider.Capabilities.HasFlag(ProviderCapabilities.ModelSelection));
    }

    [Fact]
    public void OpenCodeProvider_BuildArguments_WithTriggerWord_InjectsDetachedExecutionInstruction()
    {
        var options = Options.Create(new CliExecutionOptions
        {
            Headless = true,
            DetachedExecutionTriggerWords = ["run", "start"]
        });
        var provider = new OpenCodeProvider(options, _promptLogger, _executor);
        var context = CreateContext(null, "Please run the application in the background");

        var args = provider.BuildArguments(context);

        Assert.Contains(CliProviderBase.DetachedExecutionInstruction, args);
        Assert.Contains("Please run the application in the background", args);
        Assert.Contains("--agent agenthub", args);
    }

    [Fact]
    public void OpenCodeProvider_BuildArguments_WithoutTriggerWord_DoesNotInjectInstruction()
    {
        var options = Options.Create(new CliExecutionOptions
        {
            Headless = true,
            DetachedExecutionTriggerWords = ["run", "start"]
        });
        var provider = new OpenCodeProvider(options, _promptLogger, _executor);
        var context = CreateContext(null, "Explain the structure of this code");

        var args = provider.BuildArguments(context);

        Assert.DoesNotContain(CliProviderBase.DetachedExecutionInstruction, args);
        Assert.Contains("\"Explain the structure of this code\"", args);
    }

    [Fact]
    public void OpenCodeProvider_BuildArguments_DoesNotDuplicateIfAlreadyPresent()
    {
        var options = Options.Create(new CliExecutionOptions
        {
            Headless = true,
            DetachedExecutionTriggerWords = ["run", "start"]
        });
        var provider = new OpenCodeProvider(options, _promptLogger, _executor);
        var promptWithInstruction = $"{CliProviderBase.DetachedExecutionInstruction} run the app";
        var context = CreateContext(null, promptWithInstruction);

        var args = provider.BuildArguments(context);

        // Verify it occurs only once
        var firstIndex = args.IndexOf(CliProviderBase.DetachedExecutionInstruction, StringComparison.Ordinal);
        var lastIndex = args.LastIndexOf(CliProviderBase.DetachedExecutionInstruction, StringComparison.Ordinal);
        Assert.NotEqual(-1, firstIndex);
        Assert.Equal(firstIndex, lastIndex);
    }

    [Fact]
    public void OpenCodeProvider_GetDetachedExecutionTriggerWords_MergesGlobalAndProviderOverrides()
    {
        var globalOpts = Options.Create(new CliExecutionOptions
        {
            DetachedExecutionTriggerWords = ["run", "start"]
        });

        var providerSettings = new ProvidersOptions
        {
            ["opencode"] = new ProviderSettingsOptions
            {
                DetachedExecutionTriggerWords = ["customtrigger", "START"]
            }
        };
        var providersOpts = Options.Create(providerSettings);

        var provider = new OpenCodeProvider(globalOpts, _promptLogger, _executor, providersOpts);
        var words = provider.GetDetachedExecutionTriggerWords();

        Assert.Contains("run", words);
        Assert.Contains("start", words, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("customtrigger", words);
        Assert.Equal(3, words.Count);
    }
}
