using AIAgentHub.Domain.Common;
using AIAgentHub.Domain.Conversations;

namespace AIAgentHub.Domain.Workspaces;

public sealed class Workspace : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string Path { get; private set; } = string.Empty;
    public WorkspaceOrigin Origin { get; private set; } = WorkspaceOrigin.Server;
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastAccessedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public WorkspaceSettings Settings { get; private set; } = new();

    private readonly List<Conversation> _conversations = [];
    public IReadOnlyCollection<Conversation> Conversations => _conversations.AsReadOnly();

    private Workspace() { }

    public static Workspace Create(string name, string path, WorkspaceOrigin origin = WorkspaceOrigin.Server, WorkspaceSettings? settings = null)
    {
        return string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Workspace name cannot be empty.", nameof(name))
            : string.IsNullOrWhiteSpace(path)
            ? throw new ArgumentException("Workspace path cannot be empty.", nameof(path))
            : new Workspace
            {
                Id = Guid.NewGuid(),
                Name = name.Trim(),
                Path = System.IO.Path.GetFullPath(path.Trim()),
                Origin = origin,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastAccessedAtUtc = DateTimeOffset.UtcNow,
                Settings = settings ?? new WorkspaceSettings()
            };
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException("Workspace name cannot be empty.", nameof(newName));
        }

        Name = newName.Trim();
        Touch();
    }

    public void UpdateSettings(WorkspaceSettings settings)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Touch();
    }

    public void Touch() => LastAccessedAtUtc = DateTimeOffset.UtcNow;
}
