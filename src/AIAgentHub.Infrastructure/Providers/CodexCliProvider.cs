using System.Text.Json;
using System.Text.RegularExpressions;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;

using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public sealed class CodexCliProvider(
    IOptions<CliExecutionOptions> options,
    IPromptLogger promptLogger,
    IProcessExecutor processExecutor,
    IOptions<ProvidersOptions>? providersOptions = null) : CliProviderBase(options, promptLogger, processExecutor, providersOptions)
{
    public override string Id => "codex";
    public override string ExecutableName => "codex";
    public override string? InstallCommand => "npm install -g @openai/codex-cli";

    protected override string DefaultDisplayName => "OpenAI Codex CLI";
    protected override string DefaultDescription => "Orchestrates OpenAI Codex coding agent CLI.";
    protected override string? DefaultInstallInstructions => "Install via npm or official OpenAI distribution.";
    protected override string? DefaultAuthCommand => "auth login";
    public override ProviderCapability Capabilities =>
        ProviderCapability.Streaming | ProviderCapability.ToolCalling | ProviderCapability.FileEditing | ProviderCapability.ModelSelection;

    public override Task<string?> StartSessionAsync(Guid conversationId, string workspacePath, string? modelId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public override async Task ExecuteAsync(ProviderExecutionContext context)
    {
        var needsSessionCapture = string.IsNullOrEmpty(context.ProviderSessionId) && context.OnSessionCreated != null;
        string? capturedSessionId = null;
        var headerBuffer = new System.Text.StringBuilder();

        var wrappedContext = needsSessionCapture
            ? new ProviderExecutionContext(
                context.ConversationId,
                context.WorkspaceId,
                context.WorkspacePath,
                context.Prompt,
                context.ModelId,
                context.ProviderSessionId,
                context.IgnoredFiles,
                async token =>
                {
                    if (capturedSessionId == null)
                    {
                        _ = headerBuffer.Append(token);
                        var match = Regex.Match(headerBuffer.ToString(), @"session id:\s*([a-f0-9-]+)", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            capturedSessionId = match.Groups[1].Value.Trim();
                            await context.OnSessionCreated!(capturedSessionId);
                        }
                    }
                    await context.OnStreamToken(token);
                },
                context.RequestPermission,
                context.CancellationToken,
                context.OnSessionCreated,
                context.Effort)
            : context;

        await base.ExecuteAsync(wrappedContext);
    }

    public override string BuildArguments(ProviderExecutionContext context)
    {
        var escapedPrompt = context.Prompt.Replace("\"", "\\\"");
        var modelArg = FormatFlag("--model", context.ModelId, skipDefaultModel: true);
        var effortArg = !string.IsNullOrWhiteSpace(context.Effort)
            ? $" -c model_reasoning_effort={context.Effort.ToLowerInvariant()}"
            : string.Empty;

        return !string.IsNullOrWhiteSpace(context.ProviderSessionId)
            ? $"exec resume --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check{modelArg}{effortArg} {context.ProviderSessionId} \"{escapedPrompt}\""
            : $"exec --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check{modelArg}{effortArg} \"{escapedPrompt}\"";
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

            var result = await RunCommandAsync(exePath, "debug models", null, timeoutCts.Token, "OpenAI Codex — List Models");
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
            {
                return CreateDefaultModelList();
            }

            var parsed = ParseModelsJson(result.Output);
            return parsed.Count > 0 ? parsed : CreateDefaultModelList();
        }
        catch
        {
            return CreateDefaultModelList();
        }
    }

    public static IReadOnlyList<ModelInfo> ParseModelsJson(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<ModelInfo>();
        }

        try
        {
            var trimmed = output.Trim();
            var jsonStart = trimmed.IndexOf('{');
            var jsonEnd = trimmed.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                trimmed = trimmed[jsonStart..(jsonEnd + 1)];
            }

            using var doc = JsonDocument.Parse(trimmed);
            if (!doc.RootElement.TryGetProperty("models", out var modelsProp) || modelsProp.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<ModelInfo>();
            }

            var models = new List<ModelInfo>();
            var isFirst = true;

            foreach (var item in modelsProp.EnumerateArray())
            {
                if (!item.TryGetProperty("slug", out var slugProp) || slugProp.GetString() is not { } slug || string.IsNullOrWhiteSpace(slug))
                {
                    continue;
                }

                var displayName = item.TryGetProperty("display_name", out var dnProp) && !string.IsNullOrWhiteSpace(dnProp.GetString())
                    ? dnProp.GetString()!
                    : slug;

                var description = item.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;

                int? contextWindow = item.TryGetProperty("context_window", out var cwProp) && cwProp.TryGetInt32(out var cw)
                    ? cw
                    : null;

                var visibility = item.TryGetProperty("visibility", out var visProp) ? visProp.GetString() : null;
                var isDisplayed = !string.Equals(visibility, "hidden", StringComparison.OrdinalIgnoreCase);

                models.Add(new ModelInfo
                {
                    Id = slug,
                    DisplayName = displayName,
                    Description = description,
                    ContextWindow = contextWindow,
                    IsDefault = isFirst,
                    IsDisplayed = isDisplayed
                });

                isFirst = false;
            }

            return models;
        }
        catch
        {
            return Array.Empty<ModelInfo>();
        }
    }

    protected override IReadOnlyList<ModelInfo> CreateDefaultModelList() =>
    [
        new()
        {
            Id = "default",
            DisplayName = "Default",
            Description = "OpenAI Codex default model.",
            ContextWindow = null,
            IsDefault = true,
            IsDisplayed = true
        }
    ];
}
