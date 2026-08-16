using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using AIAgentHub.Infrastructure.Executors;

using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public sealed class GeminiCliProvider(
    IOptions<CliExecutionOptions> options,
    IPromptLogger promptLogger,
    IProcessExecutor processExecutor,
    IOptions<ProvidersOptions>? providersOptions = null) : CliProviderBase(options, promptLogger, processExecutor, providersOptions)
{
    public override string Id => "gemini";
    public override string ExecutableName => "gemini";
    public override string? InstallCommand => "npm install -g @google/antigravity";

    protected override string DefaultDisplayName => "Gemini CLI";
    protected override string DefaultDescription => "[Discontinued] Legacy Google Gemini CLI for code understanding. Discontinued by Google in favor of Antigravity.";
    protected override string? DefaultInstallInstructions => "Gemini CLI is no longer supported by Google for individuals. Please install and migrate to Antigravity CLI.";
    protected override string? DefaultAuthCommand => "auth login";
    public override ProviderCapability Capabilities => ProviderCapability.None;

    public override async Task<ProviderInfo> DetectAsync(CancellationToken cancellationToken = default)
    {
        var info = await base.DetectAsync(cancellationToken);
        info.Status = ProviderStatus.Discontinued;
        info.Message = "Discontinued: This client is no longer supported for Gemini Code Assist for individuals. Please migrate to the Antigravity suite of products.";
        return info;
    }

    public override Task<ProviderDetectionResult> DetectDetailedAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProviderDetectionResult(
            ProviderStatus.Discontinued,
            "Discontinued: This client is no longer supported for Gemini Code Assist for individuals. Please migrate to the Antigravity suite of products.",
            null
        ));
    }

    public override string BuildArguments(ProviderExecutionContext context)
    {
        var escapedPrompt = context.Prompt.Replace("\"", "\\\"");
        return $"-p \"{escapedPrompt}\"{FormatFlag("--model", context.ModelId, skipDefaultModel: true)}";
    }

    public override Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ModelInfo>>(new List<ModelInfo>
        {
            new()
            {
                Id = "discontinued",
                DisplayName = "Discontinued (Migrate to Antigravity)",
                Description = "Gemini CLI has been discontinued by Google. Please select Antigravity CLI instead.",
                ContextWindow = 0,
                IsDefault = true,
                IsDisplayed = true
            }
        });
    }
}
