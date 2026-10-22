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

        var existingChanges = await _fileChangeRepository.GetByConversationIdAsync(conversationId, cancellationToken);

        var duplicateChanges = existingChanges
            .GroupBy(c => c.RelativePath.Replace('\\', '/').TrimStart('/'), StringComparer.OrdinalIgnoreCase)
            .SelectMany(g => g.OrderByDescending(c => c.CreatedAtUtc).Skip(1))
            .ToList();

        foreach (var dup in duplicateChanges)
        {
            await _fileChangeRepository.DeleteAsync(dup, cancellationToken);
        }

        var latestChangesByPath = existingChanges
            .GroupBy(c => c.RelativePath.Replace('\\', '/').TrimStart('/'), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.CreatedAtUtc).First(), StringComparer.OrdinalIgnoreCase);

        var pendingPaths = new HashSet<string>(
            latestChangesByPath
                .Where(kv => kv.Value.Status == ReviewStatus.Pending)
                .Select(kv => kv.Key),
            StringComparer.OrdinalIgnoreCase);

        var existingSnapshots = await _snapshotRepository.GetByConversationIdAsync(conversationId, cancellationToken);
        var existingSnapshotMap = existingSnapshots
            .GroupBy(s => s.RelativePath.Replace('\\', '/').TrimStart('/'), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var acceptedDeletedPaths = new HashSet<string>(
            latestChangesByPath
                .Where(kv => kv.Value.Status == ReviewStatus.Accepted && kv.Value.ChangeType == FileChangeType.Deleted)
                .Select(kv => kv.Key),
            StringComparer.OrdinalIgnoreCase);

        await DeleteAcceptedDeletedSnapshotsAsync(snapshotDir, acceptedDeletedPaths, existingSnapshotMap, cancellationToken);

        var captureContext = new SnapshotCaptureContext(
            rootDir,
            ignored,
            pendingPaths,
            existingSnapshotMap,
            snapshotDir,
            workspaceId,
            conversationId);

        var files = rootDir.GetFiles("*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            await CaptureFileSnapshotAsync(file, captureContext, cancellationToken);
        }

        return conversationId.ToString();
    }

    private async Task DeleteAcceptedDeletedSnapshotsAsync(
        string snapshotDir,
        HashSet<string> acceptedDeletedPaths,
        Dictionary<string, List<FileSnapshot>> existingSnapshotMap,
        CancellationToken cancellationToken)
    {
        foreach (var deletedRelPath in acceptedDeletedPaths)
        {
            if (existingSnapshotMap.TryGetValue(deletedRelPath, out var oldSnapshots))
            {
                foreach (var oldSnap in oldSnapshots)
                {
                    await _snapshotRepository.DeleteAsync(oldSnap, cancellationToken);
                    TryDeleteQuietly(Path.Combine(snapshotDir, oldSnap.StorageKey));
                }
            }
        }
    }

    private async Task CaptureFileSnapshotAsync(
        FileInfo file,
        SnapshotCaptureContext ctx,
        CancellationToken cancellationToken)
    {
        if (IsIgnored(file, ctx.RootDir.FullName, ctx.Ignored))
        {
            return;
        }

        var relativePath = Path.GetRelativePath(ctx.RootDir.FullName, file.FullName).Replace('\\', '/');

        if (ctx.PendingPaths.Contains(relativePath))
        {
            return;
        }

        var fileHash = await ComputeFileHashAsync(file.FullName, cancellationToken);

        if (ctx.ExistingSnapshotMap.TryGetValue(relativePath, out var oldSnapshots))
        {
            foreach (var oldSnap in oldSnapshots)
            {
                await _snapshotRepository.DeleteAsync(oldSnap, cancellationToken);
                TryDeleteQuietly(Path.Combine(ctx.SnapshotDir, oldSnap.StorageKey));
            }
        }

        var storageKey = Guid.NewGuid().ToString("N");
        var backupDest = Path.Combine(ctx.SnapshotDir, storageKey);

        File.Copy(file.FullName, backupDest, true);

        var snapshot = FileSnapshot.Create(
            ctx.WorkspaceId,
            ctx.ConversationId,
            relativePath,
            storageKey,
            fileHash,
            file.Length
        );

        await _snapshotRepository.AddAsync(snapshot, cancellationToken);
    }

    public async Task<IReadOnlyList<FileChange>> DetectAndRecordChangesAsync(
        Guid workspaceId,
        Guid conversationId,
        string workspacePath,
        string snapshotToken,
        IReadOnlyList<string> ignoredPatterns,
        CancellationToken cancellationToken = default)
    {
        var baselineMap = await GetBaselinesByPathAsync(conversationId, cancellationToken);
        var existingChanges = await _fileChangeRepository.GetByConversationIdAsync(conversationId, cancellationToken);
        var latestChangesByPath = await DeduplicateChangesAsync(existingChanges, cancellationToken);

        var pendingChangesMap = latestChangesByPath
            .Where(kv => kv.Value.Status == ReviewStatus.Pending)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

        var acceptedDeletedPaths = new HashSet<string>(
            latestChangesByPath
                .Where(kv => kv.Value.Status == ReviewStatus.Accepted && kv.Value.ChangeType == FileChangeType.Deleted)
                .Select(kv => kv.Key),
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

        var changeContext = new ChangeDetectionContext(
            rootDir,
            baselineMap,
            pendingChangesMap,
            seenRelativePaths,
            conversationId,
            detectedChanges);

        foreach (var file in currentFiles)
        {
            await DetectFileChangeAsync(file, changeContext, cancellationToken);
        }

        await DetectDeletedChangesAsync(baselineMap, acceptedDeletedPaths, seenRelativePaths, pendingChangesMap, conversationId, detectedChanges, cancellationToken);

        return detectedChanges;
    }

    private async Task<Dictionary<string, FileSnapshot>> GetBaselinesByPathAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var baselineSnapshots = await _snapshotRepository.GetByConversationIdAsync(conversationId, cancellationToken);
        return baselineSnapshots
            .GroupBy(s => s.RelativePath.Replace('\\', '/').TrimStart('/'), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CapturedAtUtc).First(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, FileChange>> DeduplicateChangesAsync(IReadOnlyList<FileChange> existingChanges, CancellationToken cancellationToken)
    {
        var duplicateChanges = existingChanges
            .GroupBy(c => c.RelativePath.Replace('\\', '/').TrimStart('/'), StringComparer.OrdinalIgnoreCase)
            .SelectMany(g => g.OrderByDescending(c => c.CreatedAtUtc).Skip(1))
            .ToList();

        foreach (var dup in duplicateChanges)
        {
            await _fileChangeRepository.DeleteAsync(dup, cancellationToken);
        }

        return existingChanges
            .GroupBy(c => c.RelativePath.Replace('\\', '/').TrimStart('/'), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.CreatedAtUtc).First(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task DetectFileChangeAsync(
        FileInfo file,
        ChangeDetectionContext ctx,
        CancellationToken cancellationToken)
    {
        var relativePath = Path.GetRelativePath(ctx.RootDir.FullName, file.FullName).Replace('\\', '/');
        _ = ctx.SeenRelativePaths.Add(relativePath);

        var currentHash = await ComputeFileHashAsync(file.FullName, cancellationToken);

        if (ctx.PendingChangesMap.TryGetValue(relativePath, out var existingPending))
        {
            if (ctx.BaselineMap.TryGetValue(relativePath, out var baseline))
            {
                if (string.Equals(baseline.FileHash, currentHash, StringComparison.Ordinal))
                {
                    await _fileChangeRepository.DeleteAsync(existingPending, cancellationToken);
                }
                else
                {
                    ctx.DetectedChanges.Add(existingPending);
                }
            }
            else if (existingPending.ChangeType == FileChangeType.Created)
            {
                ctx.DetectedChanges.Add(existingPending);
            }
        }
        else if (ctx.BaselineMap.TryGetValue(relativePath, out var baseline))
        {
            if (!string.Equals(baseline.FileHash, currentHash, StringComparison.Ordinal))
            {
                var change = FileChange.Create(ctx.ConversationId, relativePath, FileChangeType.Modified, baseline.StorageKey);
                await _fileChangeRepository.AddAsync(change, cancellationToken);
                ctx.DetectedChanges.Add(change);
            }
        }
        else
        {
            var change = FileChange.Create(ctx.ConversationId, relativePath, FileChangeType.Created, null);
            await _fileChangeRepository.AddAsync(change, cancellationToken);
            ctx.DetectedChanges.Add(change);
        }
    }

    private async Task DetectDeletedChangesAsync(
        Dictionary<string, FileSnapshot> baselineMap,
        HashSet<string> acceptedDeletedPaths,
        HashSet<string> seenRelativePaths,
        Dictionary<string, FileChange> pendingChangesMap,
        Guid conversationId,
        List<FileChange> detectedChanges,
        CancellationToken cancellationToken)
    {
        foreach (var (relPath, baseline) in baselineMap)
        {
            if (acceptedDeletedPaths.Contains(relPath))
            {
                await _snapshotRepository.DeleteAsync(baseline, cancellationToken);
                TryDeleteQuietly(Path.Combine(GetSnapshotDir(conversationId), baseline.StorageKey));
                continue;
            }

            if (seenRelativePaths.Contains(relPath))
            {
                continue;
            }

            if (pendingChangesMap.TryGetValue(relPath, out var existingPending))
            {
                await HandlePendingDeletionAsync(existingPending, detectedChanges, cancellationToken);
            }
            else
            {
                var change = FileChange.Create(conversationId, relPath, FileChangeType.Deleted, baseline.StorageKey);
                await _fileChangeRepository.AddAsync(change, cancellationToken);
                detectedChanges.Add(change);
            }
        }
    }

    private async Task HandlePendingDeletionAsync(FileChange existingPending, List<FileChange> detectedChanges, CancellationToken cancellationToken)
    {
        if (existingPending.ChangeType == FileChangeType.Created)
        {
            await _fileChangeRepository.DeleteAsync(existingPending, cancellationToken);
            return;
        }

        if (existingPending.ChangeType != FileChangeType.Deleted)
        {
            existingPending.UpdateChangeType(FileChangeType.Deleted);
            await _fileChangeRepository.UpdateAsync(existingPending, cancellationToken);
        }
        detectedChanges.Add(existingPending);
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
            TryDeleteQuietly(Path.Combine(snapshotDir, snap.StorageKey));
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
        else if (change.ChangeType is FileChangeType.Modified or FileChangeType.Deleted && !string.IsNullOrEmpty(change.SnapshotPath))
        {
            RestoreFromSnapshot(change.ConversationId, change.SnapshotPath!, targetFullPath);
        }

        return Task.CompletedTask;
    }

    private static void RestoreFromSnapshot(Guid conversationId, string snapshotPath, string targetFullPath)
    {
        var backupFile = Path.Combine(GetSnapshotDir(conversationId), snapshotPath);
        if (!File.Exists(backupFile))
        {
            return;
        }

        var targetDir = Path.GetDirectoryName(targetFullPath);
        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
        {
            _ = Directory.CreateDirectory(targetDir);
        }

        File.Copy(backupFile, targetFullPath, true);
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

    private static void TryDeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch { /* Non-fatal: snapshot file locked or already deleted */ }
    }

    private sealed record SnapshotCaptureContext(
        DirectoryInfo RootDir,
        HashSet<string> Ignored,
        HashSet<string> PendingPaths,
        Dictionary<string, List<FileSnapshot>> ExistingSnapshotMap,
        string SnapshotDir,
        Guid WorkspaceId,
        Guid ConversationId);

    private sealed record ChangeDetectionContext(
        DirectoryInfo RootDir,
        Dictionary<string, FileSnapshot> BaselineMap,
        Dictionary<string, FileChange> PendingChangesMap,
        HashSet<string> SeenRelativePaths,
        Guid ConversationId,
        List<FileChange> DetectedChanges);
}
