using System.Runtime.InteropServices;

namespace AIAgentHub.Application.Filesystem;

public sealed class FilesystemService : IFilesystemService
{
    public Task<IReadOnlyList<DriveItem>> GetDrivesAsync(CancellationToken cancellationToken = default)
    {
        var drives = new List<DriveItem>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady)
                    {
                        drives.Add(new DriveItem(
                            drive.Name,
                            drive.RootDirectory.FullName,
                            drive.TotalSize,
                            drive.AvailableFreeSpace,
                            drive.DriveType.ToString()
                        ));
                    }
                    else
                    {
                        drives.Add(new DriveItem(
                            drive.Name,
                            drive.Name,
                            0,
                            0,
                            drive.DriveType.ToString()
                        ));
                    }
                }
                catch
                {
                    // Ignore inaccessible drive
                }
            }
        }
        else
        {
            var root = new DriveInfo("/");
            drives.Add(new DriveItem(
                "/",
                "/",
                root.IsReady ? root.TotalSize : 0,
                root.IsReady ? root.AvailableFreeSpace : 0,
                "Fixed"
            ));
        }

        return Task.FromResult<IReadOnlyList<DriveItem>>(drives);
    }

    public Task<DirectoryBrowseResult> BrowseDirectoryAsync(string? path, CancellationToken cancellationToken = default)
    {
        string targetPath;

        if (string.IsNullOrWhiteSpace(path))
        {
            targetPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(targetPath) || !Directory.Exists(targetPath))
            {
                targetPath = AppContext.BaseDirectory;
            }
        }
        else
        {
            targetPath = Path.GetFullPath(path.Trim());
        }

        if (!Directory.Exists(targetPath))
        {
            targetPath = Path.GetDirectoryName(targetPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!Directory.Exists(targetPath))
            {
                targetPath = AppContext.BaseDirectory;
            }
        }

        var dirInfo = new DirectoryInfo(targetPath);
        var entries = new List<DirectoryEntryItem>();

        try
        {
            foreach (var subDir in dirInfo.GetDirectories().OrderBy(d => d.Name))
            {
                if ((subDir.Attributes & FileAttributes.Hidden) != 0 && subDir.Name.StartsWith('.'))
                {
                    continue;
                }

                entries.Add(new DirectoryEntryItem(
                    subDir.Name,
                    subDir.FullName,
                    true,
                    null,
                    new DateTimeOffset(subDir.LastWriteTimeUtc, TimeSpan.Zero)
                ));
            }

            foreach (var file in dirInfo.GetFiles().OrderBy(f => f.Name))
            {
                entries.Add(new DirectoryEntryItem(
                    file.Name,
                    file.FullName,
                    false,
                    file.Length,
                    new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero)
                ));
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Return empty or partial if permissions restrict
        }

        var parentPath = dirInfo.Parent?.FullName;

        return Task.FromResult(new DirectoryBrowseResult(dirInfo.FullName, parentPath, entries));
    }

    public Task<TreeNodeItem> GetWorkspaceTreeAsync(string workspacePath, IReadOnlyList<string>? ignoredPatterns = null, CancellationToken cancellationToken = default)
    {
        var rootDir = new DirectoryInfo(Path.GetFullPath(workspacePath));
        if (!rootDir.Exists)
        {
            throw new DirectoryNotFoundException($"Workspace directory not found: {workspacePath}");
        }

        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".vs", "bin", "obj", "node_modules", ".idea", ".vscode"
        };

        if (ignoredPatterns != null)
        {
            foreach (var p in ignoredPatterns)
            {
                _ = ignored.Add(p);
            }
        }

        var rootNode = BuildTreeNode(rootDir, rootDir.FullName, ignored, 0, 4);
        return Task.FromResult(rootNode);
    }

    private static TreeNodeItem BuildTreeNode(DirectoryInfo dir, string workspaceRoot, HashSet<string> ignored, int currentDepth, int maxDepth)
    {
        var relativePath = Path.GetRelativePath(workspaceRoot, dir.FullName).Replace('\\', '/');
        if (relativePath == ".")
        {
            relativePath = "";
        }

        var children = new List<TreeNodeItem>();

        if (currentDepth < maxDepth)
        {
            try
            {
                foreach (var subDir in dir.GetDirectories().OrderBy(d => d.Name))
                {
                    if (ignored.Contains(subDir.Name))
                    {
                        continue;
                    }

                    children.Add(BuildTreeNode(subDir, workspaceRoot, ignored, currentDepth + 1, maxDepth));
                }

                foreach (var file in dir.GetFiles().OrderBy(f => f.Name))
                {
                    if (ignored.Contains(file.Name))
                    {
                        continue;
                    }

                    var fileRel = Path.GetRelativePath(workspaceRoot, file.FullName).Replace('\\', '/');
                    children.Add(new TreeNodeItem(file.Name, fileRel, file.FullName, false, file.Length));
                }
            }
            catch (UnauthorizedAccessException)
            {
                // ignore
            }
        }

        return new TreeNodeItem(dir.Name, relativePath, dir.FullName, true, null, children);
    }

    public async Task<byte[]> ReadFileBytesAsync(string fullPath, CancellationToken cancellationToken = default) => await File.ReadAllBytesAsync(fullPath, cancellationToken);

    public async Task<string> ReadFileTextAsync(string fullPath, CancellationToken cancellationToken = default) => await File.ReadAllTextAsync(fullPath, cancellationToken);

    public async Task WriteFileTextAsync(string fullPath, string content, CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            _ = Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(fullPath, content, cancellationToken);
    }

    public async Task WriteFileBytesAsync(string fullPath, byte[] content, CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            _ = Directory.CreateDirectory(dir);
        }

        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
    }

    public bool FileExists(string fullPath) => File.Exists(fullPath);

    public bool DirectoryExists(string fullPath) => Directory.Exists(fullPath);

    public string SuggestWorkspaceName(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Workspace";
        }

        var trimmed = path.TrimEnd('\\', '/');
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(name) ? "Workspace" : name;
    }
}
