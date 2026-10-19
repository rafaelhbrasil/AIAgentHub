using AIAgentHub.Application.Git;
using AIAgentHub.Application.Workspaces;
using AIAgentHub.Domain.Workspaces;
using AIAgentHub.Web.Controllers;

using Microsoft.AspNetCore.Mvc;

namespace AgentHub.UnitTests.Controllers;

public sealed class GitAndFilesystemControllerTests
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
            => Task.FromResult(new WorkspaceDto(Guid.NewGuid(), request.WorkspaceName ?? "Cloned", request.TargetDirectory, WorkspaceOrigin.Git, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new WorkspaceSettings(), 0));

        public Task<WorkspaceDto> UpdateAsync(Guid id, UpdateWorkspaceRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(DefaultWorkspace!);

        public Task<WorkspaceDto> SetFavoriteAsync(Guid id, bool isFavorite, CancellationToken cancellationToken = default)
            => Task.FromResult(DefaultWorkspace!);

        public Task<WorkspaceDto> SetArchivedAsync(Guid id, bool isArchived, CancellationToken cancellationToken = default)
            => Task.FromResult(DefaultWorkspace!);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task TouchAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeGitService : IGitService
    {
        public Task<GitStatusDto> GetStatusAsync(string workspacePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitStatusDto(true, true, "main", 0, 0, [], [], []));

        public Task<IReadOnlyList<GitBranchDto>> GetBranchesAsync(string workspacePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GitBranchDto>>([new GitBranchDto("main", true, false)]);

        public Task<GitOperationResult> CreateBranchAsync(string workspacePath, string branchName, bool checkout = true, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitOperationResult(true, "Branch created"));

        public Task<GitOperationResult> CheckoutBranchAsync(string workspacePath, string branchName, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitOperationResult(true, "Switched"));

        public Task<GitOperationResult> StageAsync(string workspacePath, IReadOnlyList<string>? files = null, bool stageAll = false, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitOperationResult(true, "Staged"));

        public Task<GitOperationResult> UnstageAsync(string workspacePath, IReadOnlyList<string>? files = null, bool unstageAll = false, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitOperationResult(true, "Unstaged"));

        public Task<GitOperationResult> CommitAsync(string workspacePath, string message, bool stageAll = false, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitOperationResult(true, "Committed"));

        public Task<GitOperationResult> PushAsync(string workspacePath, string? remote = null, string? branch = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitOperationResult(true, "Pushed"));

        public Task<GitOperationResult> PullAsync(string workspacePath, string? remote = null, string? branch = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitOperationResult(true, "Pulled"));

        public Task<GitOperationResult> FetchAsync(string workspacePath, string? remote = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitOperationResult(true, "Fetched"));

        public Task<GitOperationResult> StashAsync(string workspacePath, string action = "save", string? message = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitOperationResult(true, "Stashed"));

        public Task<IReadOnlyList<GitCommitDto>> GetHistoryAsync(string workspacePath, int maxCount = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GitCommitDto>>([]);

        public Func<string, string, GitCommitDetailDto?>? OnGetCommitDetails { get; set; }
        public Func<string, string, string, GitCommitDiffDto?>? OnGetCommitDiff { get; set; }

        public Task<GitCommitDetailDto?> GetCommitDetailsAsync(string workspacePath, string commitHash, CancellationToken cancellationToken = default)
            => Task.FromResult(OnGetCommitDetails?.Invoke(workspacePath, commitHash));

        public Task<GitCommitDiffDto?> GetCommitDiffAsync(string workspacePath, string commitHash, string filePath, CancellationToken cancellationToken = default)
            => Task.FromResult(OnGetCommitDiff?.Invoke(workspacePath, commitHash, filePath));

        public Task<GitOperationResult> CloneAsync(string cloneUrl, string targetDirectory, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitOperationResult(true, "Cloned"));
    }

    [Fact]
    public async Task GitController_GetStatus_ReturnsStatus()
    {
        var wsId = Guid.NewGuid();
        var wsService = new FakeWorkspaceService
        {
            DefaultWorkspace = new WorkspaceDto(wsId, "Test WS", "C:\\test\\ws", WorkspaceOrigin.Server, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new WorkspaceSettings(), 0)
        };
        var gitService = new FakeGitService();
        var controller = new GitController(gitService, wsService);

        var actionResult = await controller.GetStatus(wsId, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var status = Assert.IsType<GitStatusDto>(okResult.Value);

        Assert.True(status.IsRepository);
        Assert.Equal("main", status.CurrentBranch);
    }

    [Fact]
    public async Task GitController_CreateBranch_Succeeds()
    {
        var wsId = Guid.NewGuid();
        var wsService = new FakeWorkspaceService
        {
            DefaultWorkspace = new WorkspaceDto(wsId, "Test WS", "C:\\test\\ws", WorkspaceOrigin.Server, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new WorkspaceSettings(), 0)
        };
        var gitService = new FakeGitService();
        var controller = new GitController(gitService, wsService);

        var actionResult = await controller.CreateBranch(new GitController.CreateBranchRequest(wsId, "feature/test"), CancellationToken.None);
        Assert.IsType<OkObjectResult>(actionResult);
    }

    [Fact]
    public async Task GitController_GetCommitDetails_WhenFound_ReturnsOk()
    {
        var wsId = Guid.NewGuid();
        var wsService = new FakeWorkspaceService
        {
            DefaultWorkspace = new WorkspaceDto(wsId, "Test WS", "C:\\test\\ws", WorkspaceOrigin.Server, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new WorkspaceSettings(), 0)
        };
        var gitService = new FakeGitService
        {
            OnGetCommitDetails = (path, hash) => new GitCommitDetailDto(hash, "short", "Rafael", "r@b.com", DateTimeOffset.UtcNow, "feat: commit", [], null, [])
        };
        var controller = new GitController(gitService, wsService);

        var actionResult = await controller.GetCommitDetails(wsId, "1234abcd", CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var details = Assert.IsType<GitCommitDetailDto>(okResult.Value);
        Assert.Equal("1234abcd", details.Hash);
    }

    [Fact]
    public async Task GitController_GetCommitDetails_WhenNotFound_ReturnsNotFound()
    {
        var wsId = Guid.NewGuid();
        var wsService = new FakeWorkspaceService
        {
            DefaultWorkspace = new WorkspaceDto(wsId, "Test WS", "C:\\test\\ws", WorkspaceOrigin.Server, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new WorkspaceSettings(), 0)
        };
        var gitService = new FakeGitService();
        var controller = new GitController(gitService, wsService);

        var actionResult = await controller.GetCommitDetails(wsId, "1234abcd", CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(actionResult);
    }

    [Fact]
    public async Task GitController_GetCommitDiff_WhenFound_ReturnsOk()
    {
        var wsId = Guid.NewGuid();
        var wsService = new FakeWorkspaceService
        {
            DefaultWorkspace = new WorkspaceDto(wsId, "Test WS", "C:\\test\\ws", WorkspaceOrigin.Server, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new WorkspaceSettings(), 0)
        };
        var gitService = new FakeGitService
        {
            OnGetCommitDiff = (path, hash, file) => new GitCommitDiffDto(file, "old", "new", false)
        };
        var controller = new GitController(gitService, wsService);

        var actionResult = await controller.GetCommitDiff(wsId, "1234abcd", "src/File.cs", CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.NotNull(okResult.Value);
        var relativePath = okResult.Value.GetType().GetProperty("relativePath")?.GetValue(okResult.Value)?.ToString();
        var oldContent = okResult.Value.GetType().GetProperty("oldContent")?.GetValue(okResult.Value)?.ToString();
        var newContent = okResult.Value.GetType().GetProperty("newContent")?.GetValue(okResult.Value)?.ToString();
        Assert.Equal("src/File.cs", relativePath);
        Assert.Equal("old", oldContent);
        Assert.Equal("new", newContent);
    }

    [Fact]
    public async Task GitController_GetCommitDiff_WhenNotFound_ReturnsNotFound()
    {
        var wsId = Guid.NewGuid();
        var wsService = new FakeWorkspaceService
        {
            DefaultWorkspace = new WorkspaceDto(wsId, "Test WS", "C:\\test\\ws", WorkspaceOrigin.Server, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new WorkspaceSettings(), 0)
        };
        var gitService = new FakeGitService();
        var controller = new GitController(gitService, wsService);

        var actionResult = await controller.GetCommitDiff(wsId, "1234abcd", "missing.cs", CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(actionResult);
    }
}
