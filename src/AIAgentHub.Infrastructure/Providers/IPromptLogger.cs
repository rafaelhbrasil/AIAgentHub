using AIAgentHub.Domain.Providers;

namespace AIAgentHub.Infrastructure.Providers;

public interface IPromptLogger
{
    public bool IsEnabled { get; }
    public void LogPromptSent(string providerName, string modelName, string commandLine, int promptLength);
    public void LogCommandResult(string providerName, string operation, string commandLine, int exitCode, string? output, string? error);
    public void LogProviderStatus(string providerName, ProviderStatus status, string? message);
}
