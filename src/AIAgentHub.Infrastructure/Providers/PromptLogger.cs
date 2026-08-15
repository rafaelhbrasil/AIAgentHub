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
}
