using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;

using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public sealed class ClaudeCodeProvider(
    IOptions<CliExecutionOptions> options,
    IPromptLogger promptLogger,
    IProcessExecutor processExecutor,
    IOptions<ProvidersOptions>? providersOptions = null) : CliProviderBase(options, promptLogger, processExecutor, providersOptions)
{
    public override string Id => "claude";
    public override string ExecutableName => "claude";
    public override string? InstallCommand => "npm install -g @anthropic-ai/claude-code";

    protected override string DefaultDisplayName => "Claude Code";
    protected override string DefaultDescription => "Anthropic Claude Code CLI assistant for deep repository exploration and refactoring.";
    protected override string? DefaultInstallInstructions => "Install Claude Code CLI via npm or brew.";
    protected override string? DefaultAuthCommand => "/login";
    public override ProviderCapability Capabilities =>
        ProviderCapability.Streaming | ProviderCapability.ToolCalling | ProviderCapability.FileEditing | ProviderCapability.Skills | ProviderCapability.ModelSelection;

    protected override async Task<TestCommandResult> RunTestCommandAsync(string exePath, CancellationToken cancellationToken)
    {
        // 1. Check if ANTHROPIC_API_KEY environment variable is set
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return new TestCommandResult(true, null);
        }

        // 2. Otherwise test authentication via claude CLI auth status
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            var result = await RunCommandAsync(exePath, "auth status", null, timeoutCts.Token, "Claude Code — Auth Status Check");
            var combined = (result.Output + " " + result.Error).ToLowerInvariant();
            return combined.Contains("logged in") || combined.Contains("loggedin") || combined.Contains("authenticated") || combined.Contains("active session")
                ? new TestCommandResult(true, null)
                : result.ExitCode != 0 || combined.Contains("not logged in") || combined.Contains("unauthenticated") || combined.Contains("login required") || combined.Contains("error")
                ? new TestCommandResult(false, "Claude Code is not authenticated. Please run 'claude login' in terminal or set ANTHROPIC_API_KEY.")
                : new TestCommandResult(true, null);
        }
        catch (Exception ex)
        {
            return new TestCommandResult(false, $"Claude Code authentication check failed: {ex.Message}");
        }
    }

    public override Task<string?> StartSessionAsync(Guid conversationId, string workspacePath, string? modelId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(conversationId.ToString());

    public override string BuildArguments(ProviderExecutionContext context)
    {
        var escapedPrompt = context.Prompt.Replace("\"", "\\\"");
        var sessionArg = !string.IsNullOrWhiteSpace(context.ProviderSessionId)
            ? FormatFlag("--session-id", context.ProviderSessionId)
            : string.Empty;
        var modelArg = FormatFlag("--model", context.ModelId, skipDefaultModel: true);
        var effortArg = !string.IsNullOrWhiteSpace(context.Effort)
            ? FormatFlag("--effort", context.Effort.ToLowerInvariant())
            : string.Empty;
        return $"--output-format text --permission-mode acceptEdits -p \"{escapedPrompt}\"{modelArg}{effortArg}{sessionArg}";
    }

    public override Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDefaultModelList());

    protected override IReadOnlyList<ModelInfo> CreateDefaultModelList() =>
    [
        new()
        {
            Id = "claude-3-7-sonnet",
            DisplayName = "Claude 3.7 Sonnet",
            Description = "Most intelligent Anthropic model with hybrid reasoning.",
            ContextWindow = 200000,
            IsDefault = true,
            IsDisplayed = true
        },
        new()
        {
            Id = "claude-3-5-sonnet",
            DisplayName = "Claude 3.5 Sonnet",
            Description = "High intelligence and fast coding capabilities.",
            ContextWindow = 200000,
            IsDefault = false,
            IsDisplayed = true
        },
        new()
        {
            Id = "claude-3-5-haiku",
            DisplayName = "Claude 3.5 Haiku",
            Description = "Fastest, lightweight Anthropic model.",
            ContextWindow = 200000,
            IsDefault = false,
            IsDisplayed = true
        },
        new()
        {
            Id = "claude-3-opus",
            DisplayName = "Claude 3 Opus",
            Description = "Powerful model for complex analysis.",
            ContextWindow = 200000,
            IsDefault = false,
            IsDisplayed = true
        }
    ];
}
