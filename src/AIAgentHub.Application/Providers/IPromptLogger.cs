using AIAgentHub.Domain.Providers;

namespace AIAgentHub.Application.Providers;

public interface IPromptLogger
{
    public bool IsEnabled { get; }
    public void LogPromptExecution(Guid conversationId, string providerId, string? modelId, string prompt, bool hasHandoffPreamble, int unsharedMessageCount);
    public void LogPromptSent(string providerName, string modelName, string commandLine, int promptLength);
    public void LogCommandResult(string providerName, string operation, string commandLine, int exitCode, string? output, string? error);
    public void LogProviderStatus(string providerName, ProviderStatus status, string? message);
}
