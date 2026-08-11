namespace AIAgentHub.Domain.Workspaces;

public enum WorkspaceOrigin
{
    Server = 0,
    Remote = 1,
    Git = 2,
    Imported = 3
}

public sealed class WorkspaceSettings
{
    public string? DefaultProviderId { get; set; } = "gemini";
    public string? DefaultModelId { get; set; }
    public List<string> IgnoredFiles { get; set; } = new() { ".git", "node_modules", "bin", "obj", ".vs" };
    public bool AutoAcceptDiffs { get; set; }
}
