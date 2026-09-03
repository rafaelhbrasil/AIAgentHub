using AIAgentHub.Domain.Common;

namespace AIAgentHub.Domain.FileChanges;

public sealed class FileChange : Entity
{
    public Guid ConversationId { get; private set; }
    public string RelativePath { get; private set; } = string.Empty;
    public FileChangeType ChangeType { get; private set; } = FileChangeType.Modified;
    public string? SnapshotPath { get; private set; }
    public ReviewStatus Status { get; private set; } = ReviewStatus.Pending;
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    private FileChange() { }

    public static FileChange Create(Guid conversationId, string relativePath, FileChangeType changeType, string? snapshotPath = null)
    {
        return conversationId == Guid.Empty
            ? throw new ArgumentException("Conversation ID must be valid.", nameof(conversationId))
            : string.IsNullOrWhiteSpace(relativePath)
            ? throw new ArgumentException("Relative path cannot be empty.", nameof(relativePath))
            : new FileChange
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                RelativePath = relativePath.Replace('\\', '/').TrimStart('/'),
                ChangeType = changeType,
                SnapshotPath = snapshotPath,
                Status = ReviewStatus.Pending,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
    }

    public void Accept()
    {
        Status = ReviewStatus.Accepted;
        ReviewedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Reject()
    {
        Status = ReviewStatus.Rejected;
        ReviewedAtUtc = DateTimeOffset.UtcNow;
    }

    public void UpdateChangeType(FileChangeType changeType)
    {
        ChangeType = changeType;
    }
}
