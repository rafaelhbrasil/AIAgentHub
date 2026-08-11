using AIAgentHub.Domain.Common;

namespace AIAgentHub.Domain.FileChanges;

public sealed class FileSnapshot : Entity
{
    public Guid WorkspaceId { get; private set; }
    public Guid ConversationId { get; private set; }
    public string RelativePath { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string FileHash { get; private set; } = string.Empty;
    public long Size { get; private set; }
    public DateTimeOffset CapturedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    private FileSnapshot() { }

    public static FileSnapshot Create(Guid workspaceId, Guid conversationId, string relativePath, string storageKey, string fileHash, long size)
    {
        return new FileSnapshot
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ConversationId = conversationId,
            RelativePath = relativePath.Replace('\\', '/').TrimStart('/'),
            StorageKey = storageKey,
            FileHash = fileHash,
            Size = size,
            CapturedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
