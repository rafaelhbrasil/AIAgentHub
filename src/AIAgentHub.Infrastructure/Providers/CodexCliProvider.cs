using System.Text;
using System.Text.Json;
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
        var buffer = new StringBuilder();
        var isFirstMessage = true;
        var bufferLock = new SemaphoreSlim(1, 1);

        var wrappedContext = new ProviderExecutionContext(
            context.ConversationId,
            context.WorkspaceId,
            context.WorkspacePath,
            context.Prompt,
            context.ModelId,
            context.ProviderSessionId,
            context.IgnoredFiles,
            async chunk =>
            {
                await bufferLock.WaitAsync(context.CancellationToken);
                try
                {
                    _ = buffer.Append(chunk);
                    await ProcessBufferAsync(buffer, context, isFirst => isFirstMessage = isFirst, isFirstMessage, isFinal: false);
                }
                finally
                {
                    _ = bufferLock.Release();
                }
            },
            context.RequestPermission,
            context.CancellationToken,
            context.OnSessionCreated,
            context.Effort);

        await base.ExecuteAsync(wrappedContext);

        await bufferLock.WaitAsync(CancellationToken.None);
        try
        {
            if (buffer.Length > 0)
            {
                await ProcessBufferAsync(buffer, context, isFirst => isFirstMessage = isFirst, isFirstMessage, isFinal: true);
            }
        }
        finally
        {
            _ = bufferLock.Release();
        }
    }

    public static async Task ProcessBufferAsync(
        StringBuilder buffer,
        ProviderExecutionContext context,
        Action<bool> setIsFirstMessage,
        bool isFirstMessage,
        bool isFinal)
    {
        var text = buffer.ToString();
        var lastNewline = text.LastIndexOf('\n');
        if (lastNewline < 0 && !isFinal)
        {
            return;
        }

        var linesToProcess = isFinal
            ? text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            : text[..lastNewline].Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (!isFinal)
        {
            _ = buffer.Remove(0, lastNewline + 1);
        }
        else
        {
            _ = buffer.Clear();
        }

        foreach (var rawLine in linesToProcess)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith('{') || !line.EndsWith('}'))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var typeProp))
                {
                    continue;
                }

                var type = typeProp.GetString();
                switch (type)
                {
                    case "thread.started":
                        if (root.TryGetProperty("thread_id", out var threadIdProp) &&
                            threadIdProp.GetString() is { } threadId &&
                            !string.IsNullOrWhiteSpace(threadId) &&
                            context.OnSessionCreated != null)
                        {
                            await context.OnSessionCreated(threadId.Trim());
                        }
                        break;

                    case "item.started":
                        if (root.TryGetProperty("item", out var startItemProp))
                        {
                            var startItemType = startItemProp.TryGetProperty("type", out var stProp) ? stProp.GetString() : null;
                            if (startItemType == "command_execution" &&
                                startItemProp.TryGetProperty("command", out var cmdProp) &&
                                cmdProp.GetString() is { } cmdText &&
                                !string.IsNullOrWhiteSpace(cmdText))
                            {
                                var prefix = isFirstMessage ? "" : "\n\n";
                                setIsFirstMessage(false);
                                await context.OnStreamToken($"{prefix}⚡ **Running command:** `{cmdText}`\n");
                            }
                        }
                        break;

                    case "item.completed":
                        if (root.TryGetProperty("item", out var itemProp))
                        {
                            var itemType = itemProp.TryGetProperty("type", out var itProp) ? itProp.GetString() : null;
                            if (itemType == "agent_message" &&
                                itemProp.TryGetProperty("text", out var textProp) &&
                                textProp.GetString() is { } msgText &&
                                !string.IsNullOrEmpty(msgText))
                            {
                                var prefix = isFirstMessage ? "" : "\n\n";
                                setIsFirstMessage(false);
                                await context.OnStreamToken(prefix + msgText);
                            }
                            else if (itemType == "command_execution" &&
                                     itemProp.TryGetProperty("aggregated_output", out var outProp) &&
                                     outProp.GetString() is { } cmdOut &&
                                     !string.IsNullOrWhiteSpace(cmdOut))
                            {
                                var trimmedOut = cmdOut.Trim();
                                if (trimmedOut.Length > 0)
                                {
                                    var formattedOut = trimmedOut.Length > 3000
                                        ? trimmedOut[..3000] + "\n...(truncated)"
                                        : trimmedOut;
                                    await context.OnStreamToken($"```text\n{formattedOut}\n```\n");
                                }
                            }
                        }
                        break;

                    case "turn.failed":
                        if (root.TryGetProperty("error", out var turnErrProp))
                        {
                            var errMsg = turnErrProp.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : turnErrProp.ToString();
                            if (!string.IsNullOrWhiteSpace(errMsg))
                            {
                                var cleanErr = ExtractErrorMessage(errMsg);
                                await context.OnStreamToken($"\n\n⚠️ **Error:** {cleanErr}\n");
                            }
                        }
                        break;

                    case "error":
                        if (root.TryGetProperty("message", out var errProp) &&
                            errProp.GetString() is { } errMsgStr &&
                            !string.IsNullOrEmpty(errMsgStr))
                        {
                            var cleanErr = ExtractErrorMessage(errMsgStr);
                            await context.OnStreamToken($"\n\n⚠️ **Error:** {cleanErr}\n");
                        }
                        break;
                }
            }
            catch
            {
                // Ignore non-JSON or malformed lines
            }
        }
    }

    private static string ExtractErrorMessage(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "An error occurred during execution.";
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("message", out var msg) &&
                msg.GetString() is { } message &&
                !string.IsNullOrWhiteSpace(message))
            {
                return message;
            }
        }
        catch
        {
            // not a nested JSON string
        }

        return raw;
    }

    public override string BuildArguments(ProviderExecutionContext context)
    {
        var escapedPrompt = context.Prompt.Replace("\"", "\\\"");
        var modelArg = FormatFlag("--model", context.ModelId, skipDefaultModel: true);
        var effortArg = !string.IsNullOrWhiteSpace(context.Effort)
            ? $" -c model_reasoning_effort={context.Effort.ToLowerInvariant()}"
            : string.Empty;

        return !string.IsNullOrWhiteSpace(context.ProviderSessionId)
            ? $"exec resume {context.ProviderSessionId} --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check --json{modelArg}{effortArg} \"{escapedPrompt}\""
            : $"exec --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check --json{modelArg}{effortArg} \"{escapedPrompt}\"";
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
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(25));

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
}
