using System.Diagnostics;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;
using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public sealed class OpenCodeProvider : CliProviderBase
{
    public OpenCodeProvider(
        IOptions<CliExecutionOptions> options,
        IPromptLogger promptLogger,
        IProcessExecutor processExecutor)
        : base(options, promptLogger, processExecutor)
    {
    }

    public override string Id => "opencode";
    public override string DisplayName => "OpenCode";
    public override string Description => "Open-source provider-agnostic coding agent supporting local models (Ollama, vLLM, DeepSeek, Qwen).";

    public override string ExecutableName => "opencode";
    //public override string ExecutableName => "dotnet";

    public override string? InstallInstructions => "Install OpenCode via cargo, brew or binary release.";
    public override string? InstallCommand => "cargo install opencode-cli";
    public override string? AuthCommand => "setup";
    public override string? DocumentationUrl => "https://github.com/opencode/opencode";
    public override ProviderCapability Capabilities =>
        ProviderCapability.Streaming | ProviderCapability.ToolCalling | ProviderCapability.FileEditing | ProviderCapability.Mcp | ProviderCapability.ModelSelection;

    public override Task<string?> StartSessionAsync(Guid conversationId, string workspacePath, string? modelId, CancellationToken cancellationToken = default)
    {
        // OpenCode sessions are created dynamically by the OpenCode CLI on first run unless pre-existing
        return Task.FromResult<string?>(null);
    }

    public override async Task ExecuteAsync(ProviderExecutionContext context)
    {
        await base.ExecuteAsync(context);

        if (string.IsNullOrEmpty(context.ProviderSessionId) && context.OnSessionCreated != null)
        {
            var latestSessionId = await GetLatestSessionIdAsync(context.ConversationId, context.CancellationToken);
            if (!string.IsNullOrEmpty(latestSessionId))
            {
                await context.OnSessionCreated(latestSessionId);
            }
        }
    }

    private async Task<string?> GetLatestSessionIdAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var exePath = FindExecutable(ExecutableName);
        if (string.IsNullOrEmpty(exePath)) return null;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "session list --pure",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var titleTarget = $"agenthub-{conversationId}";
            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains(titleTarget, StringComparison.OrdinalIgnoreCase))
                {
                    var matchTarget = System.Text.RegularExpressions.Regex.Match(line, @"ses_[A-Za-z0-9]+");
                    if (matchTarget.Success) return matchTarget.Value;
                }
            }

            var match = System.Text.RegularExpressions.Regex.Match(output, @"ses_[A-Za-z0-9]+");
            if (match.Success)
            {
                return match.Value;
            }
        }
        catch { }

        return null;
    }

    public override string BuildArguments(ProviderExecutionContext context)
    {
        var modelArg = (!string.IsNullOrEmpty(context.ModelId) && !context.ModelId.Equals("Default Model", StringComparison.OrdinalIgnoreCase))
            ? $" --model \"{context.ModelId.Replace("\"", "\\\"")}\""
            : "";
        var effortArg = !string.IsNullOrEmpty(context.Effort)
            ? $" --variant \"{context.Effort.ToLowerInvariant().Replace("\"", "\\\"")}\""
            : "";
        var sessionArg = !string.IsNullOrEmpty(context.ProviderSessionId)
            ? $" --session \"{context.ProviderSessionId.Replace("\"", "\\\"")}\""
            : $" --title \"agenthub-{context.ConversationId}\"";
        var escapedPrompt = context.Prompt.Replace("\"", "\\\"");
        return $"run \"{escapedPrompt}\"{modelArg}{effortArg}{sessionArg}";
    }

    public override Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        return TryFetchDynamicModelsAsync("models", cancellationToken);
    }
}
