using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Repositories;

namespace AIAgentHub.Application.FileChanges;

public sealed class FileChangeService : IFileChangeService
{
    private readonly IFileChangeRepository _fileChangeRepository;
    private readonly ISnapshotService _snapshotService;
    private readonly IDiffEngine _diffEngine;

    public FileChangeService(
        IFileChangeRepository fileChangeRepository,
        ISnapshotService snapshotService,
        IDiffEngine diffEngine)
    {
        _fileChangeRepository = fileChangeRepository;
        _snapshotService = snapshotService;
        _diffEngine = diffEngine;
    }

    public Task<IReadOnlyList<FileChange>> GetChangesAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        return _fileChangeRepository.GetByConversationIdAsync(conversationId, cancellationToken);
    }

    public Task<FileChange?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _fileChangeRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<DiffResult> GetDiffAsync(Guid fileChangeId, string workspacePath, CancellationToken cancellationToken = default)
    {
        var change = await _fileChangeRepository.GetByIdAsync(fileChangeId, cancellationToken);
        if (change == null)
            throw new KeyNotFoundException($"File change {fileChangeId} not found.");

        var fullCurrentPath = Path.Combine(workspacePath, change.RelativePath);
        string? currentText = File.Exists(fullCurrentPath) ? await File.ReadAllTextAsync(fullCurrentPath, cancellationToken) : null;
        string? originalText = await _snapshotService.GetSnapshotContentAsync(change, cancellationToken);

        var ext = Path.GetExtension(change.RelativePath).ToLowerInvariant();
        if (IsImageExtension(ext))
        {
            return _diffEngine.CalculateImageDiff(change.RelativePath, originalText, currentText);
        }

        return _diffEngine.CalculateTextDiff(change.RelativePath, originalText, currentText);
    }

    public async Task AcceptAsync(Guid fileChangeId, CancellationToken cancellationToken = default)
    {
        var change = await _fileChangeRepository.GetByIdAsync(fileChangeId, cancellationToken);
        if (change == null)
            throw new KeyNotFoundException($"File change {fileChangeId} not found.");

        change.Accept();
        await _fileChangeRepository.UpdateAsync(change, cancellationToken);
    }

    public async Task RejectAsync(Guid fileChangeId, string workspacePath, CancellationToken cancellationToken = default)
    {
        var change = await _fileChangeRepository.GetByIdAsync(fileChangeId, cancellationToken);
        if (change == null)
            throw new KeyNotFoundException($"File change {fileChangeId} not found.");

        await _snapshotService.RollbackFileAsync(change, workspacePath, cancellationToken);
        change.Reject();
        await _fileChangeRepository.UpdateAsync(change, cancellationToken);
    }

    private static bool IsImageExtension(string ext) => ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" or ".svg";
}
