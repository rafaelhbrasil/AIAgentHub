using System.Text.RegularExpressions;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;

using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public sealed class ClaudeCodeProvider(
    IOptions<CliExecutionOptions> options,
    IPromptLogger promptLogger,
    IProcessExecutor processExecutor,
    IOptions<ProvidersOptions>? providersOptions = null) : CliProviderBase(options, promptLogger, processExecutor, providersOptions)
{
    public override string Id => "claude";
    public override string ExecutableName => "claude";
    public override string? InstallCommand => "npm install -g @anthropic-ai/claude-code";

    protected override string DefaultDisplayName => "Claude Code";
    protected override string DefaultDescription => "Anthropic Claude Code CLI assistant for deep repository exploration and refactoring.";
    protected override string? DefaultInstallInstructions => "Install Claude Code CLI via npm or brew.";
    protected override string? DefaultAuthCommand => "/login";
    public override ProviderCapability Capabilities =>
        ProviderCapability.Streaming | ProviderCapability.ToolCalling | ProviderCapability.FileEditing | ProviderCapability.Skills | ProviderCapability.ModelSelection;

    protected override async Task<TestCommandResult> RunTestCommandAsync(string exePath, CancellationToken cancellationToken)
    {
        // 1. Check if ANTHROPIC_API_KEY environment variable is set
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return new TestCommandResult(true, null);
        }

        // 2. Otherwise test authentication via claude CLI auth status
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            var result = await RunCommandAsync(exePath, "auth status", null, timeoutCts.Token, "Claude Code — Auth Status Check");
            var combined = (result.Output + " " + result.Error).ToLowerInvariant();
            return combined.Contains("logged in") || combined.Contains("loggedin") || combined.Contains("authenticated") || combined.Contains("active session")
                ? new TestCommandResult(true, null)
                : result.ExitCode != 0 || combined.Contains("not logged in") || combined.Contains("unauthenticated") || combined.Contains("login required") || combined.Contains("error")
                ? new TestCommandResult(false, "Claude Code is not authenticated. Please run 'claude login' in terminal or set ANTHROPIC_API_KEY.")
                : new TestCommandResult(true, null);
        }
        catch (Exception ex)
        {
            return new TestCommandResult(false, $"Claude Code authentication check failed: {ex.Message}");
        }
    }

    public override Task<string?> StartSessionAsync(Guid conversationId, string workspacePath, string? modelId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public override async Task ExecuteAsync(ProviderExecutionContext context)
    {
        var isNewSession = string.IsNullOrEmpty(context.ProviderSessionId);
        await base.ExecuteAsync(context);

        if (isNewSession && context.OnSessionCreated != null)
        {
            await context.OnSessionCreated(context.ConversationId.ToString());
        }
    }

    public override string BuildArguments(ProviderExecutionContext context)
    {
        var escapedPrompt = context.Prompt.Replace("\"", "\\\"");
        var sessionArg = !string.IsNullOrWhiteSpace(context.ProviderSessionId)
            ? FormatFlag("--resume", context.ProviderSessionId)
            : FormatFlag("--session-id", context.ConversationId.ToString());
        var modelArg = FormatFlag("--model", context.ModelId, skipDefaultModel: true);
        var effortArg = !string.IsNullOrWhiteSpace(context.Effort)
            ? FormatFlag("--effort", context.Effort.ToLowerInvariant())
            : string.Empty;
        return $"--output-format text --permission-mode acceptEdits -p \"{escapedPrompt}\"{modelArg}{effortArg}{sessionArg}";
    }

    public override async Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        var exePath = FindExecutable(ExecutableName);
        if (string.IsNullOrEmpty(exePath))
        {
            return CreateDefaultModelList();
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            var result = await RunCommandAsync(exePath, "-p /model", null, timeoutCts.Token, "Claude Code — List Models");
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
            {
                return CreateDefaultModelList();
            }

            var parsed = ParseModelsOutput(result.Output);
            return parsed.Count > 0 ? parsed : CreateDefaultModelList();
        }
        catch
        {
            return CreateDefaultModelList();
        }
    }

    public static IReadOnlyList<ModelInfo> ParseModelsOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<ModelInfo>();
        }

        try
        {
            string? currentModel = null;
            var currentMatch = Regex.Match(output, @"Current model:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
            if (currentMatch.Success)
            {
                currentModel = currentMatch.Groups[1].Value.Trim();
            }

            var availableMatch = Regex.Match(output, @"Available:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
            var modelIds = new List<string>();

            if (availableMatch.Success)
            {
                var rawAvailable = availableMatch.Groups[1].Value.Trim().TrimEnd('.');
                var parts = rawAvailable.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var part in parts)
                {
                    var cleanPart = part;
                    if (cleanPart.StartsWith("or ", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanPart = cleanPart[3..].Trim();
                    }

                    if (string.IsNullOrWhiteSpace(cleanPart) ||
                        cleanPart.Contains("full model ID", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!modelIds.Contains(cleanPart, StringComparer.OrdinalIgnoreCase))
                    {
                        modelIds.Add(cleanPart);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(currentModel) && !modelIds.Contains(currentModel, StringComparer.OrdinalIgnoreCase))
            {
                modelIds.Insert(0, currentModel);
            }

            if (modelIds.Count == 0)
            {
                return Array.Empty<ModelInfo>();
            }

            var result = new List<ModelInfo>();
            var defaultTarget = !string.IsNullOrWhiteSpace(currentModel) ? currentModel : "default";

            foreach (var id in modelIds)
            {
                var isDefault = string.Equals(id, defaultTarget, StringComparison.OrdinalIgnoreCase);
                var displayName = FormatDisplayName(id);

                result.Add(new ModelInfo
                {
                    Id = id,
                    DisplayName = displayName,
                    Description = $"Claude Code model: {displayName}",
                    ContextWindow = null,
                    IsDefault = isDefault,
                    IsDisplayed = true
                });
            }

            if (result.Count > 0 && !result.Any(m => m.IsDefault))
            {
                result[0].IsDefault = true;
            }

            return result;
        }
        catch
        {
            return Array.Empty<ModelInfo>();
        }
    }

    public static IReadOnlyList<string> ParseEffortOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<string>();
        }

        try
        {
            var match = Regex.Match(output, @"Usage:\s*/effort\s*<([^>]+)>", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return Array.Empty<string>();
            }

            var rawOptions = match.Groups[1].Value;
            var parts = rawOptions.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var efforts = new List<string>();

            foreach (var part in parts)
            {
                var lower = part.ToLowerInvariant();
                if (lower is "ultracode" or "ultrathink" || string.IsNullOrWhiteSpace(lower))
                {
                    continue;
                }

                if (!efforts.Contains(lower))
                {
                    efforts.Add(lower);
                }
            }

            return efforts;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public async Task<IReadOnlyList<string>> GetSupportedEffortsAsync(CancellationToken cancellationToken = default)
    {
        var exePath = FindExecutable(ExecutableName);
        if (string.IsNullOrEmpty(exePath))
        {
            return ["low", "medium", "high", "max"];
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            var result = await RunCommandAsync(exePath, "-p /effort", null, timeoutCts.Token, "Claude Code — List Efforts");
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
            {
                return ["low", "medium", "high", "max"];
            }

            var efforts = ParseEffortOutput(result.Output);
            return efforts.Count > 0 ? efforts : ["low", "medium", "high", "max"];
        }
        catch
        {
            return ["low", "medium", "high", "max"];
        }
    }

    private static string FormatDisplayName(string id)
    {
        if (id.EndsWith("[1m]", StringComparison.OrdinalIgnoreCase))
        {
            var baseName = id[..^4];
            return $"{FormatDisplayName(baseName)} [1M]";
        }

        var clean = id.Replace("-", " ").Replace("_", " ");
        if (clean.Length <= 1) return clean.ToUpperInvariant();
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(clean);
    }
}

