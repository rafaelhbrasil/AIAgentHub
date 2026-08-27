using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Infrastructure.Providers;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace AgentHub.UnitTests.Infrastructure.Providers;

public sealed class ProviderInitialPropertiesTests
{
    private readonly IOptions<CliExecutionOptions> _options = Options.Create(new CliExecutionOptions { Headless = true });
    private readonly IPromptLogger _promptLogger = Substitute.For<IPromptLogger>();
    private readonly IProcessExecutor _executor = Substitute.For<IProcessExecutor>();

    [Fact]
    public void AntigravityProvider_InitialProperties_AreCorrect()
    {
        var provider = new AntigravityProvider(_options, _promptLogger, _executor);

        Assert.Equal("antigravity", provider.Id);
        Assert.Equal("agy", provider.ExecutableName);
        Assert.Equal("npm install -g @google/antigravity", provider.InstallCommand);
        Assert.Equal("Antigravity CLI", provider.DisplayName);
        Assert.Equal("Google DeepMind Antigravity advanced agentic coding assistant and pair programmer CLI.", provider.Description);
        Assert.Equal("Install Google DeepMind Antigravity CLI from official website.", provider.InstallInstructions);
        Assert.Equal("auth login", provider.AuthCommand);
        Assert.Equal("https://antigravity.google/download#antigravity-cli", provider.DocumentationUrl);
    }

    [Fact]
    public void GeminiCliProvider_InitialProperties_AreCorrect()
    {
        var provider = new GeminiCliProvider(_options, _promptLogger, _executor);

        Assert.Equal("gemini", provider.Id);
        Assert.Equal("gemini", provider.ExecutableName);
        Assert.Equal("npm install -g @google/antigravity", provider.InstallCommand);
        Assert.Equal("Gemini CLI", provider.DisplayName);
        Assert.Equal("[Discontinued] Legacy Google Gemini CLI for code understanding. Discontinued by Google in favor of Antigravity.", provider.Description);
        Assert.Equal("Gemini CLI is no longer supported by Google for individuals. Please install and migrate to Antigravity CLI.", provider.InstallInstructions);
        Assert.Equal("auth login", provider.AuthCommand);
        Assert.Equal("https://antigravity.google", provider.DocumentationUrl);
        Assert.Equal(ProviderCapability.None, provider.Capabilities);
    }

    [Fact]
    public void CodexCliProvider_InitialProperties_AreCorrect()
    {
        var provider = new CodexCliProvider(_options, _promptLogger, _executor);

        Assert.Equal("codex", provider.Id);
        Assert.Equal("codex", provider.ExecutableName);
        Assert.Equal("npm install -g @openai/codex-cli", provider.InstallCommand);
        Assert.Equal("Codex CLI", provider.DisplayName);
        Assert.Equal("Orchestrates OpenAI Codex coding agent CLI.", provider.Description);
        Assert.Equal("Install Codex CLI via official OpenAI distribution.", provider.InstallInstructions);
        Assert.Equal("auth login", provider.AuthCommand);
        Assert.Equal("https://learn.chatgpt.com/docs/codex/cli", provider.DocumentationUrl);
    }

    [Fact]
    public void ClaudeCodeProvider_InitialProperties_AreCorrect()
    {
        var provider = new ClaudeCodeProvider(_options, _promptLogger, _executor);

        Assert.Equal("claude", provider.Id);
        Assert.Equal("claude", provider.ExecutableName);
        Assert.Equal("npm install -g @anthropic-ai/claude-code", provider.InstallCommand);
        Assert.Equal("Claude Code", provider.DisplayName);
        Assert.Equal("Anthropic Claude Code CLI assistant for deep repository exploration and refactoring.", provider.Description);
        Assert.Equal("Install Claude Code CLI from official Anthropic distribution.", provider.InstallInstructions);
        Assert.Equal("/login", provider.AuthCommand);
        Assert.Equal("https://code.claude.com/docs/en/quickstart", provider.DocumentationUrl);
    }

    [Fact]
    public void OpenCodeProvider_InitialProperties_AreCorrect()
    {
        var provider = new OpenCodeProvider(_options, _promptLogger, _executor);

        Assert.Equal("opencode", provider.Id);
        Assert.Equal("opencode", provider.ExecutableName);
        Assert.Equal("cargo install opencode-cli", provider.InstallCommand);
        Assert.Equal("OpenCode", provider.DisplayName);
        Assert.Equal("Open-source provider-agnostic coding agent supporting cloud and local models (Ollama, vLLM, DeepSeek, Qwen).", provider.Description);
        Assert.Equal("Install OpenCode Terminal via official distribution.", provider.InstallInstructions);
        Assert.Equal("setup", provider.AuthCommand);
        Assert.Equal("https://opencode.ai/download", provider.DocumentationUrl);
    }

    [Fact]
    public void GitHubCopilotProvider_InitialProperties_AreCorrect()
    {
        var provider = new GitHubCopilotProvider(_options, _promptLogger, _executor);

        Assert.Equal("copilot", provider.Id);
        Assert.Equal("copilot", provider.ExecutableName);
        Assert.Equal("npm install -g @github/copilot", provider.InstallCommand);
        Assert.Equal("GitHub Copilot", provider.DisplayName);
        Assert.Equal("GitHub Copilot CLI agent for pairing, codebase exploration, and autonomous task execution.", provider.Description);
        Assert.Equal("Install GitHub Copilot CLI via official GitHub distribution.", provider.InstallInstructions);
        Assert.Equal("login", provider.AuthCommand);
        Assert.Equal("https://github.com/features/copilot/cli/", provider.DocumentationUrl);
    }

    [Fact]
    public void Provider_WithConfigurationOverride_OverridesDefaultProperties()
    {
        var customOptions = Options.Create(new ProvidersOptions
        {
            ["antigravity"] = new ProviderSettingsOptions
            {
                DisplayName = "Custom Antigravity",
                Description = "Custom Desc",
                InstallInstructions = "Custom Instructions",
                AuthCommand = "custom auth",
                DocumentationUrl = "https://custom.antigravity.url"
            }
        });

        var provider = new AntigravityProvider(_options, _promptLogger, _executor, customOptions);

        Assert.Equal("Custom Antigravity", provider.DisplayName);
        Assert.Equal("Custom Desc", provider.Description);
        Assert.Equal("Custom Instructions", provider.InstallInstructions);
        Assert.Equal("custom auth", provider.AuthCommand);
        Assert.Equal("https://custom.antigravity.url", provider.DocumentationUrl);
    }
}
