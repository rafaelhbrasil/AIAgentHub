using System.Diagnostics;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public sealed class ClaudeCodeProvider : CliProviderBase
{
    public ClaudeCodeProvider(IOptions<CliExecutionOptions>? options = null) : base(options)
    {
    }

    public override string Id => "claude";
    public override string DisplayName => "Claude Code";
    public override string Description => "Anthropic Claude Code CLI assistant for deep repository exploration and refactoring.";
    public override string ExecutableName => "claude";
    public override string? InstallInstructions => "Install Claude Code CLI via npm or brew.";
    public override string? InstallCommand => "npm install -g @anthropic-ai/claude-code";
    public override string? AuthCommand => "login";
    public override string? DocumentationUrl => "https://docs.anthropic.com/en/docs/agents-and-tools/claude-code";
    public override ProviderCapability Capabilities =>
        ProviderCapability.Streaming | ProviderCapability.ToolCalling | ProviderCapability.FileEditing | ProviderCapability.Skills | ProviderCapability.ModelSelection;

    protected override void ConfigureStartInfo(ProcessStartInfo psi, ProviderExecutionContext context)
    {
        psi.Arguments = $"--prompt \"{context.Prompt.Replace("\"", "\\\"")}\"";
        if (!string.IsNullOrEmpty(context.ModelId) && !context.ModelId.Equals("Default Model", StringComparison.OrdinalIgnoreCase))
        {
            psi.Arguments += $" --model {context.ModelId}";
        }
    }

    public override string FormatArgumentsForShell(string exePath, ProviderExecutionContext context)
    {
        var modelArg = (!string.IsNullOrEmpty(context.ModelId) && !context.ModelId.Equals("Default Model", StringComparison.OrdinalIgnoreCase))
            ? $" --model '{context.ModelId.Replace("'", "''")}'"
            : "";
        var escapedPrompt = context.Prompt.Replace("'", "''");
        return $"& '{exePath}' --prompt '{escapedPrompt}'{modelArg}";
    }

    public override Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ModelInfo> models = new List<ModelInfo>
        {
            new() { Id = "claude-3-7-sonnet", DisplayName = "Claude 3.7 Sonnet (Hybrid Reasoning)", Description = "Hybrid reasoning and coding architecture.", ContextWindow = 200000, IsDefault = true },
            new() { Id = "claude-3-5-sonnet", DisplayName = "Claude 3.5 Sonnet", Description = "Exceptional code quality and benchmark leader.", ContextWindow = 200000, IsDefault = false },
            new() { Id = "claude-3-5-haiku", DisplayName = "Claude 3.5 Haiku", Description = "Fast lightweight coding assistant.", ContextWindow = 200000, IsDefault = false }
        };
        return Task.FromResult(models);
    }
}
