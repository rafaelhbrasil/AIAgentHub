using AIAgentHub.Domain.Common;

namespace AIAgentHub.Domain.Mcp;

public sealed class McpTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? InputSchemaJson { get; set; }
}

public sealed class McpServer : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
    public List<McpTool> Tools { get; set; } = new();
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
