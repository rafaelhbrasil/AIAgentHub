using System.Diagnostics;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public sealed class CodexCliProvider : CliProviderBase
{
    public CodexCliProvider(IOptions<CliExecutionOptions>? options = null) : base(options)
    {
    }

    public override string Id => "codex";
    public override string DisplayName => "OpenAI Codex CLI";
    public override string Description => "Orchestrates OpenAI Codex coding agent CLI.";
    public override string ExecutableName => "codex";
    public override string? InstallInstructions => "Install via npm or official OpenAI distribution.";
    public override string? InstallCommand => "npm install -g @openai/codex-cli";
    public override string? AuthCommand => "auth login";
    public override string? DocumentationUrl => "https://platform.openai.com/docs/codex";
    public override ProviderCapability Capabilities =>
        ProviderCapability.Streaming | ProviderCapability.ToolCalling | ProviderCapability.FileEditing | ProviderCapability.ModelSelection;

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
            new() { Id = "gpt-5.5", DisplayName = "GPT-5.5 (Coding Optimized)", Description = "Flagship reasoning and code generation model.", ContextWindow = 256000, IsDefault = true },
            new() { Id = "gpt-5-mini", DisplayName = "GPT-5 Mini", Description = "Fast, cost-effective coding assistant.", ContextWindow = 128000, IsDefault = false },
            new() { Id = "o3-mini", DisplayName = "OpenAI o3-mini (Reasoning)", Description = "Deep reasoning model for complex architectural refactoring.", ContextWindow = 200000, IsDefault = false }
        };
        return Task.FromResult(models);
    }
}
