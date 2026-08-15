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

        var files = rootDir.GetFiles("*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            if (IsIgnored(file, rootDir.FullName, ignored))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(rootDir.FullName, file.FullName).Replace('\\', '/');
            var fileHash = await ComputeFileHashAsync(file.FullName, cancellationToken);

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
        var baselineMap = baselineSnapshots
            .GroupBy(s => s.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CapturedAtUtc).First(), StringComparer.OrdinalIgnoreCase);

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

            if (baselineMap.TryGetValue(relativePath, out var baseline))
            {
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
            if (!seenRelativePaths.Contains(relPath))
            {
                var change = FileChange.Create(conversationId, relPath, FileChangeType.Deleted, baseline.StorageKey);
                await _fileChangeRepository.AddAsync(change, cancellationToken);
                detectedChanges.Add(change);
            }
        }

        return detectedChanges;
    }

    public Task RollbackFileAsync(FileChange change, string workspacePath, CancellationToken cancellationToken = default)
    {
        var targetFullPath = Path.Combine(workspacePath, change.RelativePath);

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
