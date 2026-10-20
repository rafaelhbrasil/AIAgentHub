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
    protected override string DefaultDescription => "Open-source provider-agnostic coding agent supporting cloud and local models (Ollama, vLLM, DeepSeek, Qwen).";
    protected override string? DefaultInstallInstructions => "Install OpenCode Terminal via official distribution.";
    protected override string? DefaultAuthCommand => "setup";
    protected override string? DefaultDocumentationUrl => "https://opencode.ai/download";
    public override ProviderCapabilities Capabilities =>
        ProviderCapabilities.Streaming | ProviderCapabilities.ToolCalling | ProviderCapabilities.FileEditing | ProviderCapabilities.Mcp | ProviderCapabilities.ModelSelection;

    public override Task<string?> StartSessionAsync(Guid conversationId, string workspacePath, string? modelId, CancellationToken cancellationToken = default) =>
        // OpenCode sessions are created dynamically by the OpenCode CLI on first run unless pre-existing
        Task.FromResult<string?>(null);

    public override async Task ExecuteAsync(ProviderExecutionContext context)
    {
        EnsureAgentConfigured();
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

            return SelectLatestSession(doc.RootElement, titleTarget, normalizedWorkspace);
        }
        catch { /* Non-fatal: session index file not found or corrupted */ }

        return null;
    }

    private static string? SelectLatestSession(JsonElement sessions, string titleTarget, string normalizedWorkspace)
    {
        string? mostRecentMatchingWorkspace = null;
        string? mostRecentOverall = null;

        foreach (var item in sessions.EnumerateArray())
        {
            var id = GetSessionId(item);
            if (id == null)
            {
                continue;
            }

            mostRecentOverall ??= id;

            if (IsTitleMatch(item, titleTarget))
            {
                return id;
            }

            var isWorkspaceMatch = IsWorkspaceMatch(item, normalizedWorkspace);
            if (isWorkspaceMatch && mostRecentMatchingWorkspace == null)
            {
                mostRecentMatchingWorkspace = id;
            }
        }

        return mostRecentMatchingWorkspace ?? mostRecentOverall;
    }

    private static string? GetSessionId(JsonElement item)
    {
        if (!item.TryGetProperty("id", out var idProp))
        {
            return null;
        }

        var id = idProp.GetString();
        return string.IsNullOrEmpty(id) || !id.StartsWith("ses_", StringComparison.OrdinalIgnoreCase) ? null : id;
    }

    private static bool IsTitleMatch(JsonElement item, string titleTarget)
    {
        var title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
        return !string.IsNullOrEmpty(title) && title.Contains(titleTarget, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWorkspaceMatch(JsonElement item, string normalizedWorkspace)
    {
        var dir = item.TryGetProperty("directory", out var dirProp) ? dirProp.GetString() : null;
        if (string.IsNullOrEmpty(dir))
        {
            return false;
        }

        var normalizedDir = Path.GetFullPath(dir).Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
        return normalizedDir == normalizedWorkspace;
    }

    public override string BuildArguments(ProviderExecutionContext context)
    {
        var sessionArg = !string.IsNullOrEmpty(context.ProviderSessionId) && context.ProviderSessionId.StartsWith("ses_", StringComparison.OrdinalIgnoreCase)
            ? FormatFlag("--session", context.ProviderSessionId)
            : FormatFlag("--title", $"agenthub-{context.ConversationId}");

        var prompt = context.Prompt;
        var triggerWords = GetDetachedExecutionTriggerWords();
        if (ShouldInjectDetachedExecutionInstruction(prompt, triggerWords))
        {
            prompt = $"{DetachedExecutionInstruction} {prompt}";
        }

        var escapedPrompt = prompt.Replace("\"", "\\\"");
        var escapedWorkspace = context.WorkspacePath.Replace("\"", "\\\"");

        var model = context.ModelId;
        if (!string.IsNullOrWhiteSpace(model) && model.Contains('\t'))
        {
            model = model.Split('\t')[0].Trim();
        }

        return $"run --dir \"{escapedWorkspace}\" --agent agenthub --auto{FormatFlag("--model", model, skipDefaultModel: true)}{FormatFlag("--variant", context.Effort?.ToLowerInvariant())}{sessionArg} \"{escapedPrompt}\"";
    }

    public override Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default) => TryFetchDynamicModelsAsync("models", cancellationToken);

    private static void EnsureAgentConfigured()
    {
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(userProfile)) return;
            var dir = Path.Combine(userProfile, ".config", "opencode", "agents");
            _ = Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "agenthub.md");
            var desiredContent = """
                ---
                description: Default execution agent for AI Agent Hub
                mode: primary
                ---
                You are an AI assistant executing tasks inside AI Agent Hub.
                When asked to run any project or application (including dotnet run, npm start, web servers, or services), NEVER run them directly as blocking foreground commands. ALWAYS launch them as detached background processes and return immediately without running sleep, wait loops, or post-startup curl/ping checks:
                - On Windows (PowerShell): Start-Process -FilePath "dotnet" -ArgumentList "run" -WindowStyle Hidden (Do NOT use -NoNewWindow for background servers, and always use dotnet run so launchSettings.json ports are respected).
                - On Linux / macOS (bash): nohup dotnet run > /dev/null 2>&1 & (or run with & in background so the command returns immediately).
                """;

            if (!File.Exists(file) || !File.ReadAllText(file).Contains("launchSettings.json", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(file, desiredContent, System.Text.Encoding.UTF8);
            }
        }
        catch { }
    }
}
