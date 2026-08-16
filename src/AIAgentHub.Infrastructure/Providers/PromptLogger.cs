using AIAgentHub.Domain.Providers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIAgentHub.Infrastructure.Providers;

public sealed class PromptLogger : IPromptLogger
{
    private readonly ILogger<PromptLogger> _logger;

    public bool IsEnabled { get; }

    public PromptLogger(ILogger<PromptLogger> logger, IConfiguration configuration)
    {
        _logger = logger;
        var configValue = configuration["AgentHub:PromptLogging:Enabled"];
        IsEnabled = string.IsNullOrEmpty(configValue) ||
                   configValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   configValue.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    public void LogPromptSent(string providerName, string modelName, string commandLine, int promptLength)
    {
        if (!IsEnabled) return;

        _logger.LogDebug(
            "Prompt sent to {ProviderName} (model: {ModelName}): \"{Command}\" length={PromptLength} chars",
            providerName,
            modelName,
            commandLine,
            promptLength);
    }

    public void LogCommandResult(string providerName, string operation, string commandLine, int exitCode, string? output, string? error)
    {
        if (!IsEnabled) return;

        if (exitCode != 0)
        {
            var combined = $"{error} {output}".ToLowerInvariant();
            if (combined.Contains("auth") || combined.Contains("unauthorized") || combined.Contains("login") || combined.Contains("not authenticated"))
            {
                _logger.LogWarning(
                    "Provider {ProviderName} unauthenticated during '{Operation}' (exit code {ExitCode}): {Error}",
                    providerName,
                    operation,
                    exitCode,
                    error ?? output);
            }
            else if (combined.Contains("quota") || combined.Contains("rate limit") || combined.Contains("429"))
            {
                _logger.LogWarning(
                    "Provider {ProviderName} quota exceeded during '{Operation}' (exit code {ExitCode}): {Error}",
                    providerName,
                    operation,
                    exitCode,
                    error ?? output);
            }
            else
            {
                _logger.LogError(
                    "Command failed for provider {ProviderName} during '{Operation}' (exit code {ExitCode}): {Error}",
                    providerName,
                    operation,
                    exitCode,
                    error ?? output);
            }
        }
        else
        {
            _logger.LogDebug(
                "Command succeeded for provider {ProviderName} ({Operation}): {CommandLine}",
                providerName,
                operation,
                commandLine);
        }
    }

    public void LogProviderStatus(string providerName, ProviderStatus status, string? message)
    {
        if (!IsEnabled) return;

        switch (status)
        {
            case ProviderStatus.Ready:
                _logger.LogInformation("Provider {ProviderName} status: Ready. {Details}", providerName, message ?? string.Empty);
                break;
            case ProviderStatus.Unauthenticated:
                _logger.LogWarning("Provider {ProviderName} status: Unauthenticated. {Details}", providerName, message ?? string.Empty);
                break;
            case ProviderStatus.QuotaExceeded:
                _logger.LogWarning("Provider {ProviderName} status: Quota Exceeded. {Details}", providerName, message ?? string.Empty);
                break;
            case ProviderStatus.NotInstalled:
                _logger.LogInformation("Provider {ProviderName} status: Not Installed. {Details}", providerName, message ?? string.Empty);
                break;
            case ProviderStatus.Discontinued:
                _logger.LogWarning("Provider {ProviderName} status: Discontinued. {Details}", providerName, message ?? string.Empty);
                break;
            case ProviderStatus.Error:
            default:
                _logger.LogError("Provider {ProviderName} status: Error. {Details}", providerName, message ?? string.Empty);
                break;
        }
    }
}
