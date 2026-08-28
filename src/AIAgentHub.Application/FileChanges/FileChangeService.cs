using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Repositories;

namespace AIAgentHub.Application.FileChanges;

public sealed class FileChangeService(
    IFileChangeRepository fileChangeRepository,
    ISnapshotService snapshotService,
    IDiffEngine diffEngine) : IFileChangeService
{
    private readonly IFileChangeRepository _fileChangeRepository = fileChangeRepository;
    private readonly ISnapshotService _snapshotService = snapshotService;
    private readonly IDiffEngine _diffEngine = diffEngine;

    public Task<IReadOnlyList<FileChange>> GetChangesAsync(Guid conversationId, CancellationToken cancellationToken = default) => _fileChangeRepository.GetByConversationIdAsync(conversationId, cancellationToken);

    public Task<FileChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _fileChangeRepository.GetByIdAsync(id, cancellationToken);

    public async Task<DiffResult> GetDiffAsync(Guid fileChangeId, string workspacePath, CancellationToken cancellationToken = default)
    {
        var change = await _fileChangeRepository.GetByIdAsync(fileChangeId, cancellationToken) ?? throw new KeyNotFoundException($"File change {fileChangeId} not found.");
        var fullCurrentPath = Path.Combine(workspacePath, change.RelativePath);
        var currentText = File.Exists(fullCurrentPath) ? await File.ReadAllTextAsync(fullCurrentPath, cancellationToken) : null;
        var originalText = await _snapshotService.GetSnapshotContentAsync(change, cancellationToken);

        var ext = Path.GetExtension(change.RelativePath).ToLowerInvariant();
        return IsImageExtension(ext)
            ? _diffEngine.CalculateImageDiff(change.RelativePath, originalText, currentText)
            : _diffEngine.CalculateTextDiff(change.RelativePath, originalText, currentText);
    }

    public async Task AcceptAsync(Guid fileChangeId, CancellationToken cancellationToken = default)
    {
        var change = await _fileChangeRepository.GetByIdAsync(fileChangeId, cancellationToken) ?? throw new KeyNotFoundException($"File change {fileChangeId} not found.");
        change.Accept();
        await _fileChangeRepository.UpdateAsync(change, cancellationToken);

        // Purge any older duplicate changes for the same file path in this conversation
        var allChanges = await _fileChangeRepository.GetByConversationIdAsync(change.ConversationId, cancellationToken);
        var normalizedPath = change.RelativePath.Replace('\\', '/').TrimStart('/');
        var olderChanges = allChanges
            .Where(c => c.Id != change.Id && string.Equals(c.RelativePath.Replace('\\', '/').TrimStart('/'), normalizedPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var old in olderChanges)
        {
            await _fileChangeRepository.DeleteAsync(old, cancellationToken);
        }

        if (change.ChangeType == FileChangeType.Deleted)
        {
            await _snapshotService.DeleteSnapshotAsync(change.ConversationId, change.RelativePath, cancellationToken);
        }
    }

    public async Task RejectAsync(Guid fileChangeId, string workspacePath, CancellationToken cancellationToken = default)
    {
        var change = await _fileChangeRepository.GetByIdAsync(fileChangeId, cancellationToken) ?? throw new KeyNotFoundException($"File change {fileChangeId} not found.");
        await _snapshotService.RollbackFileAsync(change, workspacePath, cancellationToken);
        change.Reject();
        await _fileChangeRepository.UpdateAsync(change, cancellationToken);

        // Purge any older duplicate changes for the same file path in this conversation
        var allChanges = await _fileChangeRepository.GetByConversationIdAsync(change.ConversationId, cancellationToken);
        var normalizedPath = change.RelativePath.Replace('\\', '/').TrimStart('/');
        var olderChanges = allChanges
            .Where(c => c.Id != change.Id && string.Equals(c.RelativePath.Replace('\\', '/').TrimStart('/'), normalizedPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var old in olderChanges)
        {
            await _fileChangeRepository.DeleteAsync(old, cancellationToken);
        }
    }

    private static bool IsImageExtension(string ext) => ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" or ".svg";
}
