using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;

using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public sealed class AntigravityProvider(
    IOptions<CliExecutionOptions> options,
    IPromptLogger promptLogger,
    IProcessExecutor processExecutor,
    IOptions<ProvidersOptions>? providersOptions = null) : CliProviderBase(options, promptLogger, processExecutor, providersOptions)
{
    public override string Id => "antigravity";
    public override string ExecutableName => "agy";
    public override string? InstallCommand => "npm install -g @google/antigravity";

    protected override string DefaultDisplayName => "Antigravity CLI";
    protected override string DefaultDescription => "Google DeepMind Antigravity advanced agentic coding assistant and pair programmer CLI.";
    protected override string? DefaultInstallInstructions => "Install Google DeepMind Antigravity CLI.";
    protected override string? DefaultAuthCommand => "auth login";
    public override ProviderCapability Capabilities =>
        ProviderCapability.Streaming | ProviderCapability.ToolCalling | ProviderCapability.Skills | ProviderCapability.Mcp | ProviderCapability.FileEditing | ProviderCapability.Vision | ProviderCapability.ModelSelection;

    public override async Task<ProviderInfo> DetectAsync(CancellationToken cancellationToken = default)
    {
        var info = await base.DetectAsync(cancellationToken);
        if (!info.IsInstalled)
        {
            // Fallback to checking alternative binary name 'antigravity'
            var altExe = FindExecutable("antigravity");
            if (!string.IsNullOrEmpty(altExe))
            {
                info.IsInstalled = true;
                info.IsAuthenticated = true;
                info.ExecutablePath = altExe;
            }
        }
        return info;
    }

    public override Task<string?> StartSessionAsync(Guid conversationId, string workspacePath, string? modelId, CancellationToken cancellationToken = default) =>
        // Antigravity sessions are managed by Antigravity CLI dynamically
        Task.FromResult<string?>(null);

    public override string BuildArguments(ProviderExecutionContext context)
    {
        var escapedWorkspace = context.WorkspacePath.Replace("\"", "\\\"");
        var escapedPrompt = context.Prompt.Replace("\"", "\\\"");

        var model = context.ModelId;
        if (!string.IsNullOrWhiteSpace(model))
        {
            if (model.Contains('\t'))
            {
                model = model.Split('\t')[0].Trim();
            }
        }

        var modelArg = FormatFlag("--model", model, skipDefaultModel: true);
        var effortArg = !string.IsNullOrWhiteSpace(context.Effort)
            ? FormatFlag("--effort", context.Effort.ToLowerInvariant())
            : string.Empty;
        var sessionArg = !string.IsNullOrWhiteSpace(context.ProviderSessionId) && !context.ProviderSessionId.StartsWith("agenthub-", StringComparison.OrdinalIgnoreCase)
            ? FormatFlag("--conversation", context.ProviderSessionId)
            : string.Empty;

        return $"--output-format text --add-dir \"{escapedWorkspace}\" --mode accept-edits -p \"{escapedPrompt}\"{modelArg}{effortArg}{sessionArg}";
    }

    public override Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default) => TryFetchDynamicModelsAsync("models", cancellationToken);
}
