using System.Text.RegularExpressions;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;
using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public sealed class GitHubCopilotProvider(
    IOptions<CliExecutionOptions> options,
    IPromptLogger promptLogger,
    IProcessExecutor processExecutor,
    IOptions<ProvidersOptions>? providersOptions = null) : CliProviderBase(options, promptLogger, processExecutor, providersOptions)
{
    public override string Id => "copilot";
    public override string ExecutableName => "copilot";
    public override string? InstallCommand => "npm install -g @github/copilot";

    protected override string DefaultDisplayName => "GitHub Copilot";
    protected override string DefaultDescription => "GitHub Copilot CLI agent for pairing, codebase exploration, and autonomous task execution.";
    protected override string? DefaultInstallInstructions => "Install GitHub Copilot CLI via npm: npm install -g @github/copilot";
    protected override string? DefaultAuthCommand => "login";
    public override ProviderCapability Capabilities =>
        ProviderCapability.Streaming | ProviderCapability.ToolCalling | ProviderCapability.Skills | ProviderCapability.Mcp | ProviderCapability.FileEditing | ProviderCapability.Vision | ProviderCapability.ModelSelection;

    protected override async Task<TestCommandResult> RunTestCommandAsync(string exePath, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            var result = await RunCommandAsync(exePath, "-p \"ping\" --allow-all-tools", null, timeoutCts.Token, "GitHub Copilot — Auth Check");
            var combined = (result.Output + " " + result.Error).Trim();
            var lower = combined.ToLowerInvariant();

            if (lower.Contains("no authentication information found") ||
                lower.Contains("not logged in") ||
                lower.Contains("unauthenticated") ||
                lower.Contains("auth login") ||
                lower.Contains("login required"))
            {
                return new TestCommandResult(false, string.IsNullOrWhiteSpace(combined) ? "GitHub Copilot is not authenticated. Please run 'copilot login'." : combined);
            }

            return result.ExitCode != 0 && (lower.Contains("error") || lower.Contains("unauthorized"))
                ? new TestCommandResult(false, combined)
                : new TestCommandResult(true, null);
        }
        catch (Exception ex)
        {
            return new TestCommandResult(false, $"GitHub Copilot authentication check failed: {ex.Message}");
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
        var escapedWorkspace = context.WorkspacePath.Replace("\"", "\\\"");

        var sessionArg = !string.IsNullOrWhiteSpace(context.ProviderSessionId)
            ? FormatFlag("--resume", context.ProviderSessionId)
            : FormatFlag("--session-id", context.ConversationId.ToString());

        var model = context.ModelId;
        if (!string.IsNullOrWhiteSpace(model) && model.Contains('\t'))
        {
            model = model.Split('\t')[0].Trim();
        }

        var modelArg = FormatFlag("--model", model, skipDefaultModel: true);

        return $"--output-format text --silent --allow-all-tools --add-dir \"{escapedWorkspace}\"{sessionArg}{modelArg} -p \"{escapedPrompt}\"";
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
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

            var result = await RunCommandAsync(exePath, "help config", null, timeoutCts.Token, "GitHub Copilot — List Models");
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
            {
                return CreateDefaultModelList();
            }

            var parsed = ParseModelsHelpOutput(result.Output);
            return parsed.Count > 0 ? parsed : CreateDefaultModelList();
        }
        catch
        {
            return CreateDefaultModelList();
        }
    }

    public static IReadOnlyList<ModelInfo> ParseModelsHelpOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<ModelInfo>();
        }

        try
        {
            var match = Regex.Match(output, @"`model`:\s*AI model to use[^\r\n]*\r?\n((?:\s+-\s+""[^""]+""\r?\n)+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return Array.Empty<ModelInfo>();
            }

            var rawList = match.Groups[1].Value;
            var itemMatches = Regex.Matches(rawList, @"-\s+""([^""]+)""");
            if (itemMatches.Count == 0)
            {
                return Array.Empty<ModelInfo>();
            }

            var models = new List<ModelInfo>
            {
                new()
                {
                    Id = "default",
                    DisplayName = "Default",
                    Description = "Default model. The model will not be enforced, and whatever was configured in Copilot CLI remains active.",
                    ContextWindow = null,
                    IsDefault = true,
                    IsDisplayed = true
                }
            };

            foreach (Match item in itemMatches)
            {
                var id = item.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(id) || id.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var displayName = FormatDisplayName(id);
                models.Add(new ModelInfo
                {
                    Id = id,
                    DisplayName = displayName,
                    Description = $"GitHub Copilot model: {displayName}",
                    ContextWindow = null,
                    IsDefault = false,
                    IsDisplayed = true
                });
            }

            return models;
        }
        catch
        {
            return Array.Empty<ModelInfo>();
        }
    }

    private static string FormatDisplayName(string id)
    {
        var clean = id.Replace("-", " ").Replace("_", " ");
        if (clean.Length <= 1) return clean.ToUpperInvariant();
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(clean);
    }
}
