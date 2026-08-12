using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIAgentHub.Infrastructure.Providers;

public partial class PromptLogger : IPromptLogger
{
    private readonly ILogger<PromptLogger> _logger;
    private readonly bool _enabled;

    public bool IsEnabled => _enabled;

    public PromptLogger(ILogger<PromptLogger> logger, IConfiguration configuration)
    {
        _logger = logger;
        var configValue = configuration["AgentHub:PromptLogging:Enabled"];
        _enabled = string.IsNullOrEmpty(configValue) || 
                   configValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   configValue.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    public void LogPromptSent(string providerName, string modelName, string commandLine, int promptLength)
    {
        if (!_enabled) return;

        var redactedCommand = RedactPrompt(commandLine);
        _logger.LogDebug(
            "Prompt sent to {ProviderName} (model: {ModelName}) via: \"{Command}\" length={PromptLength} chars",
            providerName,
            modelName,
            redactedCommand,
            promptLength);
    }

    private static string RedactPrompt(string commandLine)
    {
        if (string.IsNullOrEmpty(commandLine)) return commandLine;

        // Replace prompt content between quotes with <<user_prompt>>
        // Match patterns like 'prompt' or "prompt" in the command
        var result = PromptRegex().Replace(
            commandLine,
            m => $"{m.Groups[1].Value}<<user_prompt>>{m.Groups[1].Value}");

        return result;
    }

    [GeneratedRegex(@"(['""])(.*?)\1")]
    private static partial Regex PromptRegex();
}
