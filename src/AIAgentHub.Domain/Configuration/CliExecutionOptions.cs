namespace AIAgentHub.Domain.Configuration;

public sealed class CliExecutionOptions
{
    public bool Headless { get; set; } = true;
    public string Shell { get; set; } = "PowerShell";
    public int HeadedAutoCloseDelaySeconds { get; set; } = 10;
}
