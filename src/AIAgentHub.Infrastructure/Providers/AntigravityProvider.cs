using System.Diagnostics;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;
using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public sealed class AntigravityProvider : CliProviderBase
{
    public AntigravityProvider(
        IOptions<CliExecutionOptions> options,
        IPromptLogger promptLogger,
        IProcessExecutor processExecutor)
        : base(options, promptLogger, processExecutor)
    {
    }

    public override string Id => "antigravity";
    public override string DisplayName => "Antigravity CLI (agy)";
    public override string Description => "Google DeepMind Antigravity advanced agentic coding assistant and pair programmer CLI.";
    public override string ExecutableName => "agy";
    public override string? InstallInstructions => "Install Google DeepMind Antigravity CLI or run via agy.";
    public override string? InstallCommand => "npm install -g @google/antigravity";
    public override string? AuthCommand => "auth login";
    public override string? DocumentationUrl => "https://deepmind.google/technologies/antigravity";
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

    public override Task<string?> StartSessionAsync(Guid conversationId, string workspacePath, string? modelId, CancellationToken cancellationToken = default)
    {
        // Antigravity uses --conversation flag with conversation ID
        var sessionId = $"agenthub-{conversationId}";
        return Task.FromResult<string?>(sessionId);
    }

    public override string BuildArguments(ProviderExecutionContext context)
    {
        var args = $"--output-format text --add-dir \"{context.WorkspacePath.Replace("\"", "\\\"")}\" --mode accept-edits -p \"{context.Prompt.Replace("\"", "\\\"")}\"";
        if (!string.IsNullOrEmpty(context.ModelId) && !context.ModelId.Equals("Default Model", StringComparison.OrdinalIgnoreCase))
        {
            args += $" --model \"{context.ModelId.Replace("\"", "\\\"")}\"";
        }
        if (!string.IsNullOrEmpty(context.Effort))
        {
            args += $" --effort \"{context.Effort.ToLowerInvariant().Replace("\"", "\\\"")}\"";
        }
        if (!string.IsNullOrEmpty(context.ProviderSessionId))
        {
            args += $" --conversation \"{context.ProviderSessionId.Replace("\"", "\\\"")}\"";
        }
        return args;
    }

    public override Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        return TryFetchDynamicModelsAsync("models", cancellationToken);
    }
}
