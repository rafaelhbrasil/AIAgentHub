namespace AIAgentHub.Application.Filesystem;

public sealed record DriveItem(string Name, string Path, long TotalSizeBytes, long FreeSizeBytes, string DriveType);

public sealed record DirectoryEntryItem(string Name, string FullPath, bool IsDirectory, long? SizeBytes, DateTimeOffset LastModifiedUtc);

public sealed record DirectoryBrowseResult(string CurrentPath, string? ParentPath, IReadOnlyList<DirectoryEntryItem> Entries);

public sealed record TreeNodeItem(string Name, string RelativePath, string FullPath, bool IsDirectory, long? SizeBytes, List<TreeNodeItem>? Children = null);

public sealed record ZipArchiveResult(int TotalFiles, IReadOnlyList<string> FailedFiles);

public interface IFilesystemService
{
    public Task<IReadOnlyList<DriveItem>> GetDrivesAsync(CancellationToken cancellationToken = default);
    public Task<DirectoryBrowseResult> BrowseDirectoryAsync(string? path, CancellationToken cancellationToken = default);
    public Task<TreeNodeItem> GetWorkspaceTreeAsync(string workspacePath, IReadOnlyList<string>? ignoredPatterns = null, CancellationToken cancellationToken = default);
    public Task<byte[]> ReadFileBytesAsync(string fullPath, CancellationToken cancellationToken = default);
    public Task<string> ReadFileTextAsync(string fullPath, CancellationToken cancellationToken = default);
    public Task WriteFileTextAsync(string fullPath, string content, CancellationToken cancellationToken = default);
    public Task WriteFileBytesAsync(string fullPath, byte[] content, CancellationToken cancellationToken = default);
    public Task<ZipArchiveResult> WriteZipArchiveAsync(string directoryPath, Stream outputStream, IReadOnlyList<string>? ignoredPatterns = null, CancellationToken cancellationToken = default);
    public bool FileExists(string fullPath);
    public bool DirectoryExists(string fullPath);
    public string SuggestWorkspaceName(string path);
}
