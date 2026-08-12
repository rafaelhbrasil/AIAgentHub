namespace AIAgentHub.Infrastructure.Providers;

public interface IPromptLogger
{
    bool IsEnabled { get; }
    void LogPromptSent(string providerName, string modelName, string commandLine, int promptLength);
}
