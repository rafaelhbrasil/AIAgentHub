using System.Diagnostics;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public sealed class GeminiCliProvider : CliProviderBase
{
    public GeminiCliProvider(IOptions<CliExecutionOptions>? options = null) : base(options)
    {
    }

    public override string Id => "gemini";
    public override string DisplayName => "Gemini CLI";
    public override string Description => "Google Gemini CLI for multi-modal code understanding, reasoning and execution.";
    public override string ExecutableName => "gemini";
    public override string? InstallInstructions => "Install via official Gemini distribution or npm.";
    public override string? InstallCommand => "npm install -g @google/gemini-cli";
    public override string? AuthCommand => "auth login";
    public override string? DocumentationUrl => "https://ai.google.dev/gemini-api";
    public override ProviderCapability Capabilities =>
        ProviderCapability.Streaming | ProviderCapability.ToolCalling | ProviderCapability.Skills | ProviderCapability.Mcp | ProviderCapability.FileEditing | ProviderCapability.Vision | ProviderCapability.ModelSelection;

    protected override void ConfigureStartInfo(ProcessStartInfo psi, ProviderExecutionContext context)
    {
        psi.Arguments = $"-p \"{context.Prompt.Replace("\"", "\\\"")}\"";
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
        return $"& '{exePath}' -p '{escapedPrompt}'{modelArg}";
    }

    public override Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ModelInfo> models = new List<ModelInfo>
        {
            new() { Id = "gemini-2.5-pro", DisplayName = "Gemini 2.5 Pro", Description = "State-of-the-art coding and massive 2M token context.", ContextWindow = 2097152, IsDefault = true },
            new() { Id = "gemini-2.5-flash", DisplayName = "Gemini 2.5 Flash", Description = "Ultra-fast low-latency code assistant.", ContextWindow = 1048576, IsDefault = false },
            new() { Id = "gemini-3.6-flash", DisplayName = "Gemini 3.6 Flash (High)", Description = "Next generation high-throughput agentic coder.", ContextWindow = 1048576, IsDefault = false }
        };
        return Task.FromResult(models);
    }
}
