using System.Diagnostics;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public sealed class AntigravityProvider : CliProviderBase
{
    public AntigravityProvider(IOptions<CliExecutionOptions>? options = null) : base(options)
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

    protected override void ConfigureStartInfo(ProcessStartInfo psi, ProviderExecutionContext context)
    {
        psi.Arguments = $"--output-format text --add-dir \"{context.WorkspacePath}\" --mode accept-edits -p \"{context.Prompt.Replace("\"", "\\\"")}\"";
        if (!string.IsNullOrEmpty(context.ModelId) && !context.ModelId.Equals("Default Model", StringComparison.OrdinalIgnoreCase))
        {
            psi.Arguments += $" --model \"{context.ModelId}\"";
        }
        if (!string.IsNullOrEmpty(context.Effort))
        {
            psi.Arguments += $" --effort \"{context.Effort.ToLowerInvariant()}\"";
        }
        if (!string.IsNullOrEmpty(context.ProviderSessionId))
        {
            psi.Arguments += $" --conversation {context.ProviderSessionId}";
        }
    }

    public override string FormatArgumentsForShell(string exePath, ProviderExecutionContext context)
    {
        var modelArg = (!string.IsNullOrEmpty(context.ModelId) && !context.ModelId.Equals("Default Model", StringComparison.OrdinalIgnoreCase))
            ? $" --model '{context.ModelId.Replace("'", "''")}'"
            : "";
        var effortArg = !string.IsNullOrEmpty(context.Effort)
            ? $" --effort '{context.Effort.ToLowerInvariant().Replace("'", "''")}'"
            : "";
        var sessionArg = !string.IsNullOrEmpty(context.ProviderSessionId) ? $" --conversation '{context.ProviderSessionId.Replace("'", "''")}'" : "";
        var escapedPrompt = context.Prompt.Replace("'", "''");
        var escapedWorkspace = context.WorkspacePath.Replace("'", "''");
        return $"& '{exePath}' --output-format text --add-dir '{escapedWorkspace}' --mode accept-edits -p '{escapedPrompt}'{modelArg}{effortArg}{sessionArg}";
    }

    public override Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ModelInfo> models = new List<ModelInfo>
        {
            new() { Id = "Gemini 3.6 Flash (High)", DisplayName = "Gemini 3.6 Flash (High Effort)", Description = "Fast multi-modal agentic model with high reasoning effort.", ContextWindow = 1048576, IsDefault = true },
            new() { Id = "Gemini 3.6 Flash (Medium)", DisplayName = "Gemini 3.6 Flash (Medium Effort)", Description = "Balanced speed and reasoning.", ContextWindow = 1048576, IsDefault = false },
            new() { Id = "Gemini 3.6 Flash (Low)", DisplayName = "Gemini 3.6 Flash (Low Effort)", Description = "Ultra-low latency code generation.", ContextWindow = 1048576, IsDefault = false },
            new() { Id = "Gemini 3.5 Flash (High)", DisplayName = "Gemini 3.5 Flash (High Effort)", Description = "Standard high reasoning flash model.", ContextWindow = 1048576, IsDefault = false },
            new() { Id = "Gemini 3.5 Flash (Medium)", DisplayName = "Gemini 3.5 Flash (Medium Effort)", Description = "Standard balanced flash model.", ContextWindow = 1048576, IsDefault = false },
            new() { Id = "Gemini 3.5 Flash (Low)", DisplayName = "Gemini 3.5 Flash (Low Effort)", Description = "Fastest turnaround flash model.", ContextWindow = 1048576, IsDefault = false },
            new() { Id = "Gemini 3.1 Pro (High)", DisplayName = "Gemini 3.1 Pro (High Effort)", Description = "Deep architectural analysis and large-scale refactoring.", ContextWindow = 2097152, IsDefault = false },
            new() { Id = "Gemini 3.1 Pro (Low)", DisplayName = "Gemini 3.1 Pro (Low Effort)", Description = "Efficient pro reasoning.", ContextWindow = 2097152, IsDefault = false },
            new() { Id = "Claude Sonnet 4.6 (Thinking)", DisplayName = "Claude Sonnet 4.6 (Thinking)", Description = "Anthropic reasoning model for precise code execution.", ContextWindow = 200000, IsDefault = false },
            new() { Id = "Claude Opus 4.6 (Thinking)", DisplayName = "Claude Opus 4.6 (Thinking)", Description = "Complex reasoning and system design.", ContextWindow = 200000, IsDefault = false },
            new() { Id = "GPT-OSS 120B (Medium)", DisplayName = "GPT-OSS 120B (Medium Effort)", Description = "High-parameter open weights model.", ContextWindow = 131072, IsDefault = false }
        };
        return Task.FromResult(models);
    }
}
