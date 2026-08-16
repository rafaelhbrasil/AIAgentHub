namespace AIAgentHub.Domain.Providers;

[Flags]
public enum ProviderCapability
{
    None = 0,
    Streaming = 1 << 0,
    ToolCalling = 1 << 1,
    Mcp = 1 << 2,
    Skills = 1 << 3,
    FileEditing = 1 << 4,
    Vision = 1 << 5,
    CommandExecution = 1 << 6,
    ModelSelection = 1 << 7
}

public enum ProviderStatus
{
    NotInstalled = 0,
    Unauthenticated = 1,
    Ready = 2,
    Error = 3,
    Running = 4,
    QuotaExceeded = 5,
    Discontinued = 6
}
