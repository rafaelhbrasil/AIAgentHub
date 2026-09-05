using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;

using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public sealed class AntigravityPtyProvider(
    IOptions<CliExecutionOptions> options,
    IPromptLogger promptLogger,
    IProcessExecutor processExecutor,
    IPtySessionManager ptySessionManager,
    IOptions<ProvidersOptions>? providersOptions = null) : CliProviderBase(options, promptLogger, processExecutor, providersOptions)
{
    private readonly IPtySessionManager _ptySessionManager = ptySessionManager;

    public override string Id => "antigravity2";
    public override string ExecutableName => "agy";
    public override string? InstallCommand => "npm install -g @google/antigravity";

    protected override string DefaultDisplayName => "Antigravity 2.0 (PTY Console)";
    protected override string DefaultDescription => "Interactive Pseudo-Terminal (PTY) console with live bi-directional streaming, background daemon isolation, and reasoning token extraction.";
    protected override string? DefaultInstallInstructions => "Install Google DeepMind Antigravity CLI from official website.";
    protected override string? DefaultAuthCommand => "auth login";
    protected override string? DefaultDocumentationUrl => "https://antigravity.google/download#antigravity-cli";
    public override ProviderCapability Capabilities =>
        ProviderCapability.Streaming | ProviderCapability.ToolCalling | ProviderCapability.Skills | ProviderCapability.Mcp | ProviderCapability.FileEditing | ProviderCapability.Vision | ProviderCapability.ModelSelection;

    public override bool IsInstalledFastCheck() =>
        !string.IsNullOrEmpty(FindExecutable(ExecutableName)) || !string.IsNullOrEmpty(FindExecutable("antigravity"));

    public override async Task<ProviderInfo> DetectAsync(CancellationToken cancellationToken = default)
    {
        var info = await base.DetectAsync(cancellationToken);
        if (!info.IsInstalled)
        {
            var altExe = FindExecutable("antigravity");
            if (!string.IsNullOrEmpty(altExe))
            {
                info.IsInstalled = true;
                info.IsAuthenticated = true;
                info.ExecutablePath = altExe;
            }
        }
        return info;
    }

    public override Task<string?> StartSessionAsync(Guid conversationId, string workspacePath, string? modelId, CancellationToken cancellationToken = default)
    {
        _ = _ptySessionManager.GetOrCreateSessionAsync(conversationId, workspacePath, cancellationToken);
        return Task.FromResult<string?>(null);
    }

    public override async Task ExecuteAsync(ProviderExecutionContext context)
    {
        _ = await _ptySessionManager.GetOrCreateSessionAsync(context.ConversationId, context.WorkspacePath, context.CancellationToken);

        var agyExe = FindExecutable("agy") ?? FindExecutable("antigravity") ?? "agy.exe";
        var escapedWorkspace = context.WorkspacePath.Replace("\"", "\\\"");
        var escapedPrompt = context.Prompt.Replace("\"", "\\\"");

        var model = context.ModelId;
        if (!string.IsNullOrWhiteSpace(model) && model.Contains('\t'))
        {
            model = model.Split('\t')[0].Trim();
        }

        var modelArg = FormatFlag("--model", model, skipDefaultModel: true);
        var effortArg = !string.IsNullOrWhiteSpace(context.Effort)
            ? FormatFlag("--effort", context.Effort.ToLowerInvariant())
            : string.Empty;
        var sessionArg = !string.IsNullOrWhiteSpace(context.ProviderSessionId) && !context.ProviderSessionId.StartsWith("agenthub-", StringComparison.OrdinalIgnoreCase)
            ? FormatFlag("--conversation", context.ProviderSessionId)
            : string.Empty;

        // Command sent to the persistent PTY interactive shell session
        var cmd = $"& '{agyExe}' --output-format text --add-dir \"{escapedWorkspace}\" --mode accept-edits -p \"{escapedPrompt}\"{modelArg}{effortArg}{sessionArg}\r\n";

        await _ptySessionManager.SendInputAsync(context.ConversationId, cmd, context.CancellationToken);

        await context.OnStreamToken($"[Antigravity 2.0] Interactive command dispatched to console:\n`{context.Prompt}`\n\n*(Check the Terminal Console tab for live interactive execution)*\n");
    }

    public override string BuildArguments(ProviderExecutionContext context)
    {
        return string.Empty;
    }
}
