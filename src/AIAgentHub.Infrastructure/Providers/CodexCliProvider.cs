using System.Diagnostics;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;
using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public sealed class CodexCliProvider : CliProviderBase
{
    public CodexCliProvider(
        IOptions<CliExecutionOptions> options,
        IPromptLogger promptLogger,
        IProcessExecutor processExecutor)
        : base(options, promptLogger, processExecutor)
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

    public override string BuildArguments(ProviderExecutionContext context)
    {
        var modelArg = (!string.IsNullOrEmpty(context.ModelId) && !context.ModelId.Equals("Default Model", StringComparison.OrdinalIgnoreCase))
            ? $" --model \"{context.ModelId.Replace("\"", "\\\"")}\""
            : "";
        var escapedPrompt = context.Prompt.Replace("\"", "\\\"");
        return $"--prompt \"{escapedPrompt}\"{modelArg}";
    }

    public override Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        return TryFetchDynamicModelsAsync("models", cancellationToken);
    }
}
