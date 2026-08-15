namespace AIAgentHub.Infrastructure.Providers;

public interface IPromptLogger
{
    public bool IsEnabled { get; }
    public void LogPromptSent(string providerName, string modelName, string commandLine, int promptLength);
}
