namespace AIAgentHub.Application.Filesystem;

public sealed record DriveItem(string Name, string Path, long TotalSizeBytes, long FreeSizeBytes, string DriveType);

public sealed record DirectoryEntryItem(string Name, string FullPath, bool IsDirectory, long? SizeBytes, DateTimeOffset LastModifiedUtc);

public sealed record DirectoryBrowseResult(string CurrentPath, string? ParentPath, IReadOnlyList<DirectoryEntryItem> Entries);

public sealed record TreeNodeItem(string Name, string RelativePath, string FullPath, bool IsDirectory, long? SizeBytes, List<TreeNodeItem>? Children = null);

public interface IFilesystemService
{
    Task<IReadOnlyList<DriveItem>> GetDrivesAsync(CancellationToken cancellationToken = default);
    Task<DirectoryBrowseResult> BrowseDirectoryAsync(string? path, CancellationToken cancellationToken = default);
    Task<TreeNodeItem> GetWorkspaceTreeAsync(string workspacePath, IReadOnlyList<string>? ignoredPatterns = null, CancellationToken cancellationToken = default);
    Task<byte[]> ReadFileBytesAsync(string fullPath, CancellationToken cancellationToken = default);
    Task<string> ReadFileTextAsync(string fullPath, CancellationToken cancellationToken = default);
    Task WriteFileTextAsync(string fullPath, string content, CancellationToken cancellationToken = default);
    Task WriteFileBytesAsync(string fullPath, byte[] content, CancellationToken cancellationToken = default);
    bool FileExists(string fullPath);
    bool DirectoryExists(string fullPath);
    string SuggestWorkspaceName(string path);
}
