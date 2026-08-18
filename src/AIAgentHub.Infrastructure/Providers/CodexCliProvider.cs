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
        Task.FromResult<string?>(conversationId.ToString());

    public override string BuildArguments(ProviderExecutionContext context)
    {
        var escapedPrompt = context.Prompt.Replace("\"", "\\\"");
        var sessionArg = !string.IsNullOrWhiteSpace(context.ProviderSessionId)
            ? FormatFlag("--session", context.ProviderSessionId)
            : string.Empty;
        return $"--prompt \"{escapedPrompt}\"{FormatFlag("--model", context.ModelId, skipDefaultModel: true)}{sessionArg}";
    }

    public override async Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        var dynamicModels = await TryFetchDynamicModelsAsync("models", cancellationToken);
        return dynamicModels.Count > 0 ? dynamicModels : CreateDefaultModelList();
    }

    protected override IReadOnlyList<ModelInfo> CreateDefaultModelList() =>
    [
        new()
        {
            Id = "o3-mini",
            DisplayName = "o3-mini",
            Description = "OpenAI high-speed reasoning model for coding and STEM.",
            ContextWindow = 200000,
            IsDefault = true,
            IsDisplayed = true
        },
        new()
        {
            Id = "o1",
            DisplayName = "o1",
            Description = "OpenAI advanced reasoning model for complex engineering tasks.",
            ContextWindow = 200000,
            IsDefault = false,
            IsDisplayed = true
        },
        new()
        {
            Id = "gpt-4o",
            DisplayName = "GPT-4o",
            Description = "OpenAI flagship multimodal fast intelligent model.",
            ContextWindow = 128000,
            IsDefault = false,
            IsDisplayed = true
        },
        new()
        {
            Id = "gpt-4o-mini",
            DisplayName = "GPT-4o mini",
            Description = "Lightweight and efficient model for fast iterations.",
            ContextWindow = 128000,
            IsDefault = false,
            IsDisplayed = true
        }
    ];
}
