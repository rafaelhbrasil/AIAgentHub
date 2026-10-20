using System.Text;
using AIAgentHub.Application.Filesystem;
using AIAgentHub.Application.Workspaces;
using AIAgentHub.Domain.Workspaces;
using AIAgentHub.Web.Controllers;

using Microsoft.AspNetCore.Mvc;

namespace AgentHub.UnitTests.Controllers;

public sealed class FilesystemControllerTests
{
    private sealed class FakeWorkspaceService : IWorkspaceService
    {
        public WorkspaceDto? DefaultWorkspace { get; set; }

        public Task<IReadOnlyList<WorkspaceDto>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkspaceDto>>(DefaultWorkspace != null ? [DefaultWorkspace] : []);

        public Task<WorkspaceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(DefaultWorkspace?.Id == id ? DefaultWorkspace : null);

        public Task<WorkspaceDto> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(DefaultWorkspace!);

        public Task<WorkspaceDto> CloneAsync(CloneWorkspaceRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(DefaultWorkspace!);

        public Task<WorkspaceDto> UpdateAsync(Guid id, UpdateWorkspaceRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(DefaultWorkspace!);

        public Task<WorkspaceDto> SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken = default)
            => Task.FromResult(DefaultWorkspace!);

        public Task<WorkspaceDto> SetArchivedAsync(Guid id, bool isArchived, CancellationToken cancellationToken = default)
            => Task.FromResult(DefaultWorkspace!);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task TouchAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSystemPathValidator : ISystemPathValidator
    {
        public IReadOnlyList<string> ForbiddenFolders => ["C:\\Windows", "C:\\Program Files"];
        public bool IsForbiddenBrowsingResult { get; set; }
        public string? ForbiddenReason { get; set; }

        public bool IsForbiddenForWorkspace(string? path, out string? reason)
        {
            reason = ForbiddenReason;
            return IsForbiddenBrowsingResult;
        }

        public bool IsForbiddenForBrowsing(string? path, out string? reason)
        {
            reason = ForbiddenReason;
            return IsForbiddenBrowsingResult;
        }

        public bool IsPathForbidden(string? path, out string? reason)
        {
            reason = ForbiddenReason;
            return IsForbiddenBrowsingResult;
        }
    }

    private sealed class FakeFilesystemService : IFilesystemService
    {
        public bool ExistsFile { get; set; } = true;
        public bool ExistsDirectory { get; set; } = true;
        public byte[] FileBytes { get; set; } = Encoding.UTF8.GetBytes("hello world");

        public Task<IReadOnlyList<DriveItem>> GetDrivesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DriveItem>>([new DriveItem("C:\\", "C:\\", 1000, 500, "Fixed")]);

        public Task<DirectoryBrowseResult> BrowseDirectoryAsync(string? path, CancellationToken cancellationToken = default)
            => Task.FromResult(new DirectoryBrowseResult(path ?? "C:\\", null, []));

        public Task<TreeNodeItem> GetWorkspaceTreeAsync(string workspacePath, IReadOnlyList<string>? ignoredPatterns = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new TreeNodeItem("root", "", workspacePath, true, null, []));

        public Task<byte[]> ReadFileBytesAsync(string fullPath, CancellationToken cancellationToken = default)
            => Task.FromResult(FileBytes);

        public Task<string> ReadFileTextAsync(string fullPath, CancellationToken cancellationToken = default)
            => Task.FromResult(Encoding.UTF8.GetString(FileBytes));

        public Task WriteFileTextAsync(string fullPath, string content, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task WriteFileBytesAsync(string fullPath, byte[] content, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CreateFileAsync(string fullPath, string? content = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CreateDirectoryAsync(string fullPath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RenameItemAsync(string oldFullPath, string newFullPath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteItemAsync(string fullPath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MoveItemAsync(string sourceFullPath, string destinationDirectoryFullPath, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ZipArchiveResult> WriteZipArchiveAsync(string directoryPath, Stream outputStream, IReadOnlyList<string>? ignoredPatterns = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ZipArchiveResult(0, []));

        public bool FileExists(string fullPath) => ExistsFile;
        public bool DirectoryExists(string fullPath) => ExistsDirectory;
        public string SuggestWorkspaceName(string path) => "suggested";
    }

    private static (FilesystemController Controller, FakeWorkspaceService Ws, FakeFilesystemService Fs, FakeSystemPathValidator Val) CreateSut()
    {
        var ws = new FakeWorkspaceService();
        var fs = new FakeFilesystemService();
        var val = new FakeSystemPathValidator();
        var controller = new FilesystemController(fs, ws, val);
        return (controller, ws, fs, val);
    }

    [Fact]
    public void GetForbiddenPaths_ReturnsList()
    {
        var (sut, _, _, _) = CreateSut();
        var result = sut.GetForbiddenPaths();
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task GetDrives_ReturnsDrives()
    {
        var (sut, _, _, _) = CreateSut();
        var result = await sut.GetDrives(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var drives = Assert.IsAssignableFrom<IReadOnlyList<DriveItem>>(ok.Value);
        _ = Assert.Single(drives);
    }

    [Fact]
    public async Task Browse_WhenForbidden_ReturnsBadRequest()
    {
        var (sut, _, _, val) = CreateSut();
        val.IsForbiddenBrowsingResult = true;
        val.ForbiddenReason = "Access to system directory is restricted";

        var result = await sut.Browse("C:\\Windows", CancellationToken.None);
        _ = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Browse_WhenAllowed_ReturnsOk()
    {
        var (sut, _, _, _) = CreateSut();
        var result = await sut.Browse("C:\\MyFolder", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        _ = Assert.IsType<DirectoryBrowseResult>(ok.Value);
    }

    [Fact]
    public async Task GetTree_WhenWorkspaceNotFound_ReturnsNotFound()
    {
        var (sut, _, _, _) = CreateSut();
        var result = await sut.GetTree(Guid.NewGuid(), CancellationToken.None);
        _ = Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetTree_WhenWorkspaceExists_ReturnsOk()
    {
        var (sut, ws, _, _) = CreateSut();
        var wsId = Guid.NewGuid();
        ws.DefaultWorkspace = new WorkspaceDto(wsId, "ws", "C:\\ws", WorkspaceOrigin.Server, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new WorkspaceSettings(), 0);

        var result = await sut.GetTree(wsId, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        _ = Assert.IsType<TreeNodeItem>(ok.Value);
    }

    [Fact]
    public async Task GetFile_WhenPathTraversal_ReturnsBadRequest()
    {
        var (sut, ws, _, _) = CreateSut();
        var wsId = Guid.NewGuid();
        ws.DefaultWorkspace = new WorkspaceDto(wsId, "ws", "C:\\ws", WorkspaceOrigin.Server, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new WorkspaceSettings(), 0);

        var result = await sut.GetFile(wsId, "../../secret.txt", CancellationToken.None);
        _ = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetFile_WhenFileNotFound_ReturnsNotFound()
    {
        var (sut, ws, fs, _) = CreateSut();
        var wsId = Guid.NewGuid();
        ws.DefaultWorkspace = new WorkspaceDto(wsId, "ws", "C:\\ws", WorkspaceOrigin.Server, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new WorkspaceSettings(), 0);
        fs.ExistsFile = false;

        var result = await sut.GetFile(wsId, "nonexistent.txt", CancellationToken.None);
        _ = Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetFile_WhenValidTextFile_ReturnsOk()
    {
        var (sut, ws, fs, _) = CreateSut();
        var wsId = Guid.NewGuid();
        ws.DefaultWorkspace = new WorkspaceDto(wsId, "ws", "C:\\ws", WorkspaceOrigin.Server, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new WorkspaceSettings(), 0);
        fs.ExistsFile = true;
        fs.FileBytes = Encoding.UTF8.GetBytes("sample content");

        var result = await sut.GetFile(wsId, "file.txt", CancellationToken.None);
        _ = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task CreateFile_WhenValid_ReturnsOk()
    {
        var (sut, ws, _, _) = CreateSut();
        var wsId = Guid.NewGuid();
        ws.DefaultWorkspace = new WorkspaceDto(wsId, "ws", "C:\\ws", WorkspaceOrigin.Server, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new WorkspaceSettings(), 0);

        var result = await sut.CreateFile(new FilesystemController.FileContentRequest(wsId, "newfile.txt", "content"), CancellationToken.None);
        _ = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task SaveFile_WhenValid_ReturnsOk()
    {
        var (sut, ws, _, _) = CreateSut();
        var wsId = Guid.NewGuid();
        ws.DefaultWorkspace = new WorkspaceDto(wsId, "ws", "C:\\ws", WorkspaceOrigin.Server, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new WorkspaceSettings(), 0);

        var result = await sut.SaveFile(new FilesystemController.FileContentRequest(wsId, "saved.txt", "updated"), CancellationToken.None);
        _ = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task RenameItem_WhenValid_ReturnsOk()
    {
        var (sut, ws, _, _) = CreateSut();
        var wsId = Guid.NewGuid();
        ws.DefaultWorkspace = new WorkspaceDto(wsId, "ws", "C:\\ws", WorkspaceOrigin.Server, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new WorkspaceSettings(), 0);

        var result = await sut.RenameItem(new FilesystemController.RenameItemRequest(wsId, "old.txt", "new.txt"), CancellationToken.None);
        _ = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task DeleteItem_WhenRoot_ReturnsBadRequest()
    {
        var (sut, ws, _, _) = CreateSut();
        var wsId = Guid.NewGuid();
        ws.DefaultWorkspace = new WorkspaceDto(wsId, "ws", "C:\\ws", WorkspaceOrigin.Server, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new WorkspaceSettings(), 0);

        var result = await sut.DeleteItem(null, wsId, "", CancellationToken.None);
        _ = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteItem_WhenValidFile_ReturnsOk()
    {
        var (sut, ws, _, _) = CreateSut();
        var wsId = Guid.NewGuid();
        ws.DefaultWorkspace = new WorkspaceDto(wsId, "ws", "C:\\ws", WorkspaceOrigin.Server, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new WorkspaceSettings(), 0);

        var result = await sut.DeleteItem(new FilesystemController.DeleteItemRequest(wsId, "sub/file.txt"), null, null, CancellationToken.None);
        _ = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task MoveItem_WhenValid_ReturnsOk()
    {
        var (sut, ws, _, _) = CreateSut();
        var wsId = Guid.NewGuid();
        ws.DefaultWorkspace = new WorkspaceDto(wsId, "ws", "C:\\ws", WorkspaceOrigin.Server, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new WorkspaceSettings(), 0);

        var result = await sut.MoveItem(new FilesystemController.MoveItemRequest(wsId, "source.txt", "destFolder"), CancellationToken.None);
        _ = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task CreateDirectory_InWorkspace_ReturnsOk()
    {
        var (sut, ws, _, _) = CreateSut();
        var wsId = Guid.NewGuid();
        ws.DefaultWorkspace = new WorkspaceDto(wsId, "ws", "C:\\ws", WorkspaceOrigin.Server, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new WorkspaceSettings(), 0);

        var result = await sut.CreateDirectory(new FilesystemController.CreateDirectoryRequest("newdir", wsId), CancellationToken.None);
        _ = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task CreateDirectory_EmptyPath_ReturnsBadRequest()
    {
        var (sut, _, _, _) = CreateSut();
        var result = await sut.CreateDirectory(new FilesystemController.CreateDirectoryRequest(""), CancellationToken.None);
        _ = Assert.IsType<BadRequestObjectResult>(result);
    }
}
