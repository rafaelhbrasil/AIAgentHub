using System.Diagnostics;
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Configuration;
using AIAgentHub.Domain.Providers;
using Microsoft.Extensions.Options;

namespace AIAgentHub.Infrastructure.Providers;

public sealed class OpenCodeProvider : CliProviderBase
{
    public OpenCodeProvider(IOptions<CliExecutionOptions>? options = null) : base(options)
    {
    }

    public override string Id => "opencode";
    public override string DisplayName => "OpenCode";
    public override string Description => "Open-source provider-agnostic coding agent supporting local models (Ollama, vLLM, DeepSeek, Qwen).";
    public override string ExecutableName => "opencode";
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

    protected override void ConfigureStartInfo(ProcessStartInfo psi, ProviderExecutionContext context)
    {
        psi.Arguments = $"run \"{context.Prompt.Replace("\"", "\\\"")}\"";
        if (!string.IsNullOrEmpty(context.ModelId) && !context.ModelId.Equals("Default Model", StringComparison.OrdinalIgnoreCase))
        {
            psi.Arguments += $" --model {context.ModelId}";
        }
        if (!string.IsNullOrEmpty(context.Effort))
        {
            psi.Arguments += $" --variant {context.Effort.ToLowerInvariant()}";
        }
        if (!string.IsNullOrEmpty(context.ProviderSessionId))
        {
            psi.Arguments += $" --session {context.ProviderSessionId}";
        }
        else
        {
            psi.Arguments += $" --title \"agenthub-{context.ConversationId}\"";
        }
    }

    public override string FormatArgumentsForShell(string exePath, ProviderExecutionContext context)
    {
        var modelArg = (!string.IsNullOrEmpty(context.ModelId) && !context.ModelId.Equals("Default Model", StringComparison.OrdinalIgnoreCase))
            ? $" --model '{context.ModelId.Replace("'", "''")}'"
            : "";
        var effortArg = !string.IsNullOrEmpty(context.Effort)
            ? $" --variant '{context.Effort.ToLowerInvariant().Replace("'", "''")}'"
            : "";
        var sessionArg = !string.IsNullOrEmpty(context.ProviderSessionId)
            ? $" --session '{context.ProviderSessionId.Replace("'", "''")}'"
            : $" --title 'agenthub-{context.ConversationId}'";
        var escapedPrompt = context.Prompt.Replace("'", "''");
        return $"& '{exePath}' run '{escapedPrompt}'{modelArg}{effortArg}{sessionArg}";
    }

    public override async Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        var exePath = FindExecutable(ExecutableName);
        if (!string.IsNullOrEmpty(exePath))
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "models",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var dynamicModels = new List<ModelInfo>();
                bool isFirst = true;

                foreach (var rawLine in lines)
                {
                    var modelLine = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(modelLine) || modelLine.StartsWith("Usage", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Clean human-friendly display name
                    var parts = modelLine.Split('/');
                    var cleanName = parts.Length > 1 ? parts[1] : modelLine;
                    cleanName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleanName.Replace("-", " ").Replace("_", " "));

                    dynamicModels.Add(new ModelInfo
                    {
                        Id = modelLine,
                        DisplayName = $"{cleanName} ({modelLine})",
                        Description = $"OpenCode CLI model: {modelLine}",
                        ContextWindow = 131072,
                        IsDefault = isFirst
                    });

                    isFirst = false;
                }

                if (dynamicModels.Count > 0)
                {
                    return dynamicModels;
                }
            }
            catch { }
        }

        // Fallback catalog if CLI model listing is unavailable
        IReadOnlyList<ModelInfo> fallbackModels = new List<ModelInfo>
        {
            new() { Id = "opencode/gemini-3.6-flash", DisplayName = "Gemini 3.6 Flash (opencode/gemini-3.6-flash)", Description = "Latest Google Flash model via OpenCode CLI.", ContextWindow = 1048576, IsDefault = true },
            new() { Id = "opencode/claude-sonnet-4-6", DisplayName = "Claude Sonnet 4.6 (opencode/claude-sonnet-4-6)", Description = "Anthropic Sonnet 4.6 reasoning model.", ContextWindow = 200000, IsDefault = false },
            new() { Id = "opencode/deepseek-v4-flash", DisplayName = "DeepSeek V4 Flash (opencode/deepseek-v4-flash)", Description = "Top open weights coding model.", ContextWindow = 131072, IsDefault = false },
            new() { Id = "opencode/gpt-5.5", DisplayName = "GPT-5.5 (opencode/gpt-5.5)", Description = "Flagship coding model.", ContextWindow = 256000, IsDefault = false }
        };
        return fallbackModels;
    }
}
