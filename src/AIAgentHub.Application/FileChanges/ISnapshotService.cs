using AIAgentHub.Domain.FileChanges;

namespace AIAgentHub.Application.FileChanges;

public interface ISnapshotService
{
    public Task<string> CaptureWorkspaceSnapshotAsync(Guid workspaceId, Guid conversationId, string workspacePath, IReadOnlyList<string> ignoredPatterns, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<FileChange>> DetectAndRecordChangesAsync(Guid workspaceId, Guid conversationId, string workspacePath, string snapshotToken, IReadOnlyList<string> ignoredPatterns, CancellationToken cancellationToken = default);
    public Task RollbackFileAsync(FileChange change, string workspacePath, CancellationToken cancellationToken = default);
    public Task<string?> GetSnapshotContentAsync(FileChange change, CancellationToken cancellationToken = default);
    public Task DeleteSnapshotAsync(Guid conversationId, string relativePath, CancellationToken cancellationToken = default);
}

public interface IFileChangeService
{
    public Task<IReadOnlyList<FileChange>> GetChangesAsync(Guid conversationId, CancellationToken cancellationToken = default);
    public Task<FileChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    public Task<DiffResult> GetDiffAsync(Guid fileChangeId, string workspacePath, CancellationToken cancellationToken = default);
    public Task AcceptAsync(Guid fileChangeId, CancellationToken cancellationToken = default);
    public Task RejectAsync(Guid fileChangeId, string workspacePath, CancellationToken cancellationToken = default);
}
