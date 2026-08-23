using System.Text.Json;

using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;

using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public sealed class OpenCodeProvider(
    IOptions<CliExecutionOptions> options,
    IPromptLogger promptLogger,
    IProcessExecutor processExecutor,
    IOptions<ProvidersOptions>? providersOptions = null) : CliProviderBase(options, promptLogger, processExecutor, providersOptions)
{
    public override string Id => "opencode";
    public override string ExecutableName => "opencode";
    public override string? InstallCommand => "cargo install opencode-cli";

    protected override string DefaultDisplayName => "OpenCode";
    protected override string DefaultDescription => "Open-source provider-agnostic coding agent supporting local models (Ollama, vLLM, DeepSeek, Qwen).";
    protected override string? DefaultInstallInstructions => "Install OpenCode via cargo, brew or binary release.";
    protected override string? DefaultAuthCommand => "setup";
    public override ProviderCapability Capabilities =>
        ProviderCapability.Streaming | ProviderCapability.ToolCalling | ProviderCapability.FileEditing | ProviderCapability.Mcp | ProviderCapability.ModelSelection;

    public override Task<string?> StartSessionAsync(Guid conversationId, string workspacePath, string? modelId, CancellationToken cancellationToken = default) =>
        // OpenCode sessions are created dynamically by the OpenCode CLI on first run unless pre-existing
        Task.FromResult<string?>(null);

    public override async Task ExecuteAsync(ProviderExecutionContext context)
    {
        await base.ExecuteAsync(context);

        if (string.IsNullOrEmpty(context.ProviderSessionId) && context.OnSessionCreated != null)
        {
            var latestSessionId = await GetLatestSessionIdAsync(context.ConversationId, context.WorkspacePath, context.CancellationToken);
            if (!string.IsNullOrEmpty(latestSessionId))
            {
                await context.OnSessionCreated(latestSessionId);
            }
        }
    }

    private async Task<string?> GetLatestSessionIdAsync(Guid conversationId, string workspacePath, CancellationToken cancellationToken)
    {
        var exePath = FindExecutable(ExecutableName);
        if (string.IsNullOrEmpty(exePath)) return null;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            var result = await RunCommandAsync(exePath, "session list --format json -n 20", workspacePath, timeoutCts.Token, "OpenCode — Session List");
            var output = result.Output;

            if (string.IsNullOrWhiteSpace(output)) return null;

            // In headed mode, output might include runner header/footer lines before/after JSON
            var jsonStart = output.IndexOf('[');
            var jsonEnd = output.LastIndexOf(']');
            if (jsonStart >= 0 && jsonEnd > jsonStart) output = output[jsonStart..(jsonEnd + 1)];

            using var doc = JsonDocument.Parse(output);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

            var normalizedWorkspace = Path.GetFullPath(workspacePath).Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
            var titleTarget = $"agenthub-{conversationId}";

            string? mostRecentMatchingWorkspace = null;
            string? mostRecentOverall = null;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("id", out var idProp))
                {
                    continue;
                }

                var id = idProp.GetString();
                if (string.IsNullOrEmpty(id) || !id.StartsWith("ses_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                mostRecentOverall ??= id;

                var title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
                if (!string.IsNullOrEmpty(title) && title.Contains(titleTarget, StringComparison.OrdinalIgnoreCase))
                {
                    return id;
                }

                var dir = item.TryGetProperty("directory", out var dirProp) ? dirProp.GetString() : null;
                if (!string.IsNullOrEmpty(dir))
                {
                    var normalizedDir = Path.GetFullPath(dir).Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
                    if (normalizedDir == normalizedWorkspace)
                    {
                        mostRecentMatchingWorkspace ??= id;
                    }
                }
            }

            return mostRecentMatchingWorkspace ?? mostRecentOverall;
        }
        catch { }

        return null;
    }

    public override string BuildArguments(ProviderExecutionContext context)
    {
        var sessionArg = !string.IsNullOrEmpty(context.ProviderSessionId) && context.ProviderSessionId.StartsWith("ses_", StringComparison.OrdinalIgnoreCase)
            ? FormatFlag("--session", context.ProviderSessionId)
            : FormatFlag("--title", $"agenthub-{context.ConversationId}");

        var escapedPrompt = context.Prompt.Replace("\"", "\\\"");
        var escapedWorkspace = context.WorkspacePath.Replace("\"", "\\\"");

        var model = context.ModelId;
        if (!string.IsNullOrWhiteSpace(model) && model.Contains('\t'))
        {
            model = model.Split('\t')[0].Trim();
        }

        return $"run --dir \"{escapedWorkspace}\" --auto{FormatFlag("--model", model, skipDefaultModel: true)}{FormatFlag("--variant", context.Effort?.ToLowerInvariant())}{sessionArg} \"{escapedPrompt}\"";
    }

    public override Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default) => TryFetchDynamicModelsAsync("models", cancellationToken);
}
