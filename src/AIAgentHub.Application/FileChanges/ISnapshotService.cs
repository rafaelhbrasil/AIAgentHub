using AIAgentHub.Domain.FileChanges;

namespace AIAgentHub.Application.FileChanges;

public interface ISnapshotService
{
    Task<string> CaptureWorkspaceSnapshotAsync(Guid workspaceId, Guid conversationId, string workspacePath, IReadOnlyList<string> ignoredPatterns, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FileChange>> DetectAndRecordChangesAsync(Guid workspaceId, Guid conversationId, string workspacePath, string snapshotToken, IReadOnlyList<string> ignoredPatterns, CancellationToken cancellationToken = default);
    Task RollbackFileAsync(FileChange change, string workspacePath, CancellationToken cancellationToken = default);
    Task<string?> GetSnapshotContentAsync(FileChange change, CancellationToken cancellationToken = default);
}

public interface IFileChangeService
{
    Task<IReadOnlyList<FileChange>> GetChangesAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<FileChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DiffResult> GetDiffAsync(Guid fileChangeId, string workspacePath, CancellationToken cancellationToken = default);
    Task AcceptAsync(Guid fileChangeId, CancellationToken cancellationToken = default);
    Task RejectAsync(Guid fileChangeId, string workspacePath, CancellationToken cancellationToken = default);
}
