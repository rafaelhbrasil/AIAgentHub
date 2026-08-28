using System.Security.Cryptography;

using AIAgentHub.Application.FileChanges;
using AIAgentHub.Domain.FileChanges;
using AIAgentHub.Domain.Repositories;

namespace AIAgentHub.Infrastructure.Snapshots;

public sealed class LocalDiskSnapshotStore(
    IFileSnapshotRepository snapshotRepository,
    IFileChangeRepository fileChangeRepository) : ISnapshotService
{
    private readonly IFileSnapshotRepository _snapshotRepository = snapshotRepository;
    private readonly IFileChangeRepository _fileChangeRepository = fileChangeRepository;

    private static string GetSnapshotDir(Guid conversationId)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(localAppData, "AIAgentHub", "Snapshots", conversationId.ToString("N"));
        if (!Directory.Exists(dir))
        {
            _ = Directory.CreateDirectory(dir);
        }

        return dir;
    }

    public async Task<string> CaptureWorkspaceSnapshotAsync(
        Guid workspaceId,
        Guid conversationId,
        string workspacePath,
        IReadOnlyList<string> ignoredPatterns,
        CancellationToken cancellationToken = default)
    {
        var snapshotDir = GetSnapshotDir(conversationId);
        var rootDir = new DirectoryInfo(Path.GetFullPath(workspacePath));
        if (!rootDir.Exists)
        {
            return conversationId.ToString();
        }

        var ignored = new HashSet<string>(ignoredPatterns, StringComparer.OrdinalIgnoreCase)
        {
            ".git", "node_modules", "bin", "obj", ".vs", ".idea", ".vscode"
        };

        // Query existing pending changes so we preserve the original pre-prompt baseline snapshot
        var existingChanges = await _fileChangeRepository.GetByConversationIdAsync(conversationId, cancellationToken);
        var pendingPaths = new HashSet<string>(
            existingChanges
                .Where(c => c.Status == ReviewStatus.Pending)
                .Select(c => c.RelativePath.Replace('\\', '/').TrimStart('/')),
            StringComparer.OrdinalIgnoreCase);

        var existingSnapshots = await _snapshotRepository.GetByConversationIdAsync(conversationId, cancellationToken);
        var existingSnapshotMap = existingSnapshots
            .GroupBy(s => s.RelativePath.Replace('\\', '/').TrimStart('/'), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Clean up snapshots for files that have been accepted as deleted
        var acceptedDeletedPaths = new HashSet<string>(
            existingChanges
                .Where(c => c.Status == ReviewStatus.Accepted && c.ChangeType == FileChangeType.Deleted)
                .Select(c => c.RelativePath.Replace('\\', '/').TrimStart('/')),
            StringComparer.OrdinalIgnoreCase);

        foreach (var deletedRelPath in acceptedDeletedPaths)
        {
            if (existingSnapshotMap.TryGetValue(deletedRelPath, out var oldSnapshots))
            {
                foreach (var oldSnap in oldSnapshots)
                {
                    await _snapshotRepository.DeleteAsync(oldSnap, cancellationToken);
                    var oldBackup = Path.Combine(snapshotDir, oldSnap.StorageKey);
                    try { if (File.Exists(oldBackup)) File.Delete(oldBackup); } catch { }
                }
            }
        }

        var files = rootDir.GetFiles("*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            if (IsIgnored(file, rootDir.FullName, ignored))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(rootDir.FullName, file.FullName).Replace('\\', '/');

            // If this file already has an active pending change, preserve the original baseline snapshot!
            if (pendingPaths.Contains(relativePath))
            {
                continue;
            }

            var fileHash = await ComputeFileHashAsync(file.FullName, cancellationToken);

            // Clean up any older snapshots for this non-pending file so only the latest baseline is kept
            if (existingSnapshotMap.TryGetValue(relativePath, out var oldSnapshots))
            {
                foreach (var oldSnap in oldSnapshots)
                {
                    await _snapshotRepository.DeleteAsync(oldSnap, cancellationToken);
                    var oldBackup = Path.Combine(snapshotDir, oldSnap.StorageKey);
                    try { if (File.Exists(oldBackup)) File.Delete(oldBackup); } catch { }
                }
            }

            var storageKey = Guid.NewGuid().ToString("N");
            var backupDest = Path.Combine(snapshotDir, storageKey);

            File.Copy(file.FullName, backupDest, true);

            var snapshot = FileSnapshot.Create(
                workspaceId,
                conversationId,
                relativePath,
                storageKey,
                fileHash,
                file.Length
            );

            await _snapshotRepository.AddAsync(snapshot, cancellationToken);
        }

        return conversationId.ToString();
    }

    public async Task<IReadOnlyList<FileChange>> DetectAndRecordChangesAsync(
        Guid workspaceId,
        Guid conversationId,
        string workspacePath,
        string snapshotToken,
        IReadOnlyList<string> ignoredPatterns,
        CancellationToken cancellationToken = default)
    {
        var baselineSnapshots = await _snapshotRepository.GetByConversationIdAsync(conversationId, cancellationToken);
        // Use the latest baseline snapshot captured for each relative path
        var baselineMap = baselineSnapshots
            .GroupBy(s => s.RelativePath.Replace('\\', '/').TrimStart('/'), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CapturedAtUtc).First(), StringComparer.OrdinalIgnoreCase);

        var existingChanges = await _fileChangeRepository.GetByConversationIdAsync(conversationId, cancellationToken);

        // Clean up any historical duplicate pending changes for the same relative path
        var duplicatePending = existingChanges
            .Where(c => c.Status == ReviewStatus.Pending)
            .GroupBy(c => c.RelativePath.Replace('\\', '/').TrimStart('/'), StringComparer.OrdinalIgnoreCase)
            .SelectMany(g => g.OrderByDescending(c => c.CreatedAtUtc).Skip(1))
            .ToList();

        foreach (var dup in duplicatePending)
        {
            await _fileChangeRepository.DeleteAsync(dup, cancellationToken);
        }

        var pendingChangesMap = existingChanges
            .Where(c => c.Status == ReviewStatus.Pending)
            .GroupBy(c => c.RelativePath.Replace('\\', '/').TrimStart('/'), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.CreatedAtUtc).First(), StringComparer.OrdinalIgnoreCase);

        var acceptedDeletedPaths = new HashSet<string>(
            existingChanges
                .Where(c => c.Status == ReviewStatus.Accepted && c.ChangeType == FileChangeType.Deleted)
                .Select(c => c.RelativePath.Replace('\\', '/').TrimStart('/')),
            StringComparer.OrdinalIgnoreCase);

        var rootDir = new DirectoryInfo(Path.GetFullPath(workspacePath));
        var ignored = new HashSet<string>(ignoredPatterns, StringComparer.OrdinalIgnoreCase)
        {
            ".git", "node_modules", "bin", "obj", ".vs", ".idea", ".vscode"
        };

        var currentFiles = rootDir.Exists
            ? [.. rootDir.GetFiles("*", SearchOption.AllDirectories).Where(f => !IsIgnored(f, rootDir.FullName, ignored))]
            : new List<FileInfo>();

        var detectedChanges = new List<FileChange>();
        var seenRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in currentFiles)
        {
            var relativePath = Path.GetRelativePath(rootDir.FullName, file.FullName).Replace('\\', '/');
            _ = seenRelativePaths.Add(relativePath);

            var currentHash = await ComputeFileHashAsync(file.FullName, cancellationToken);

            if (pendingChangesMap.TryGetValue(relativePath, out var existingPending))
            {
                // File was already pending from previous prompt
                if (baselineMap.TryGetValue(relativePath, out var baseline))
                {
                    if (string.Equals(baseline.FileHash, currentHash, StringComparison.Ordinal))
                    {
                        // File has been reverted back to baseline exactly! Remove pending change.
                        await _fileChangeRepository.DeleteAsync(existingPending, cancellationToken);
                    }
                    else
                    {
                        // Still modified from original baseline - retain single change record
                        detectedChanges.Add(existingPending);
                    }
                }
                else if (existingPending.ChangeType == FileChangeType.Created)
                {
                    // Created file still exists
                    detectedChanges.Add(existingPending);
                }
            }
            else if (baselineMap.TryGetValue(relativePath, out var baseline))
            {
                // First time this file was modified
                if (!string.Equals(baseline.FileHash, currentHash, StringComparison.Ordinal))
                {
                    var change = FileChange.Create(conversationId, relativePath, FileChangeType.Modified, baseline.StorageKey);
                    await _fileChangeRepository.AddAsync(change, cancellationToken);
                    detectedChanges.Add(change);
                }
            }
            else
            {
                // New created file
                var change = FileChange.Create(conversationId, relativePath, FileChangeType.Created, null);
                await _fileChangeRepository.AddAsync(change, cancellationToken);
                detectedChanges.Add(change);
            }
        }

        // Check for deleted files
        foreach (var (relPath, baseline) in baselineMap)
        {
            if (acceptedDeletedPaths.Contains(relPath))
            {
                // Deletion was already accepted by user in a previous turn - clean up any stale snapshot and ignore
                await _snapshotRepository.DeleteAsync(baseline, cancellationToken);
                var backup = Path.Combine(GetSnapshotDir(conversationId), baseline.StorageKey);
                try { if (File.Exists(backup)) File.Delete(backup); } catch { }
                continue;
            }

            if (!seenRelativePaths.Contains(relPath))
            {
                if (pendingChangesMap.TryGetValue(relPath, out var existingPending))
                {
                    if (existingPending.ChangeType == FileChangeType.Created)
                    {
                        // Created in this cycle then deleted -> clean up
                        await _fileChangeRepository.DeleteAsync(existingPending, cancellationToken);
                    }
                    else
                    {
                        detectedChanges.Add(existingPending);
                    }
                }
                else
                {
                    var change = FileChange.Create(conversationId, relPath, FileChangeType.Deleted, baseline.StorageKey);
                    await _fileChangeRepository.AddAsync(change, cancellationToken);
                    detectedChanges.Add(change);
                }
            }
        }

        return detectedChanges;
    }

    public async Task DeleteSnapshotAsync(Guid conversationId, string relativePath, CancellationToken cancellationToken = default)
    {
        var cleanRel = relativePath.Replace('\\', '/').TrimStart('/');
        var snapshots = await _snapshotRepository.GetByConversationIdAsync(conversationId, cancellationToken);
        var matching = snapshots.Where(s => string.Equals(s.RelativePath.Replace('\\', '/').TrimStart('/'), cleanRel, StringComparison.OrdinalIgnoreCase));
        var snapshotDir = GetSnapshotDir(conversationId);

        foreach (var snap in matching)
        {
            await _snapshotRepository.DeleteAsync(snap, cancellationToken);
            var backup = Path.Combine(snapshotDir, snap.StorageKey);
            try { if (File.Exists(backup)) File.Delete(backup); } catch { }
        }
    }

    public Task RollbackFileAsync(FileChange change, string workspacePath, CancellationToken cancellationToken = default)
    {
        var workspaceRoot = Path.GetFullPath(workspacePath);
        var cleanRel = change.RelativePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        var targetFullPath = Path.GetFullPath(Path.Combine(workspaceRoot, cleanRel));

        if (!targetFullPath.StartsWith(workspaceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !targetFullPath.Equals(workspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Target path '{targetFullPath}' escapes workspace root '{workspaceRoot}'.");
        }

        if (change.ChangeType == FileChangeType.Created)
        {
            if (File.Exists(targetFullPath))
            {
                File.Delete(targetFullPath);
            }
        }
        else if (change.ChangeType is FileChangeType.Modified or FileChangeType.Deleted)
        {
            if (!string.IsNullOrEmpty(change.SnapshotPath))
            {
                var snapshotDir = GetSnapshotDir(change.ConversationId);
                var backupFile = Path.Combine(snapshotDir, change.SnapshotPath);

                if (File.Exists(backupFile))
                {
                    var targetDir = Path.GetDirectoryName(targetFullPath);
                    if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                    {
                        _ = Directory.CreateDirectory(targetDir);
                    }

                    File.Copy(backupFile, targetFullPath, true);
                }
            }
        }

        return Task.CompletedTask;
    }

    public async Task<string?> GetSnapshotContentAsync(FileChange change, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(change.SnapshotPath))
        {
            return null;
        }

        var snapshotDir = GetSnapshotDir(change.ConversationId);
        var backupFile = Path.Combine(snapshotDir, change.SnapshotPath);

        return !File.Exists(backupFile) ? null : await File.ReadAllTextAsync(backupFile, cancellationToken);
    }

    private static bool IsIgnored(FileInfo file, string rootPath, HashSet<string> ignored)
    {
        var rel = Path.GetRelativePath(rootPath, file.FullName);
        var segments = rel.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(ignored.Contains);
    }

    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }
}
