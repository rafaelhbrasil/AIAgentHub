using System.Diagnostics;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;
using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public sealed class GeminiCliProvider : CliProviderBase
{
    public GeminiCliProvider(
        IOptions<CliExecutionOptions> options,
        IPromptLogger promptLogger,
        IProcessExecutor processExecutor)
        : base(options, promptLogger, processExecutor)
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

    public override string BuildArguments(ProviderExecutionContext context)
    {
        var modelArg = (!string.IsNullOrEmpty(context.ModelId) && !context.ModelId.Equals("Default Model", StringComparison.OrdinalIgnoreCase))
            ? $" --model \"{context.ModelId.Replace("\"", "\\\"")}\""
            : "";
        var escapedPrompt = context.Prompt.Replace("\"", "\\\"");
        return $"-p \"{escapedPrompt}\"{modelArg}";
    }

    public override Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        return TryFetchDynamicModelsAsync("models", cancellationToken);
    }
}
