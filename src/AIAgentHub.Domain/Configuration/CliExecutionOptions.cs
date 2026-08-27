namespace AIAgentHub.Domain.Configuration;

public sealed class CliExecutionOptions
{
    public bool Headless { get; set; } = true;
    public string Shell { get; set; } = "PowerShell";
    public int HeadedAutoCloseDelaySeconds { get; set; } = 10;
    public int TimeoutMinutes { get; set; } = 10;
    public int HeartbeatIntervalSeconds { get; set; } = 60;
    public bool AutoResumeOnTimeout { get; set; } = true;
    public int MaxAutoResumes { get; set; } = 2;
}
