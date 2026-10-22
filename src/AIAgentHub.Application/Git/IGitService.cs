namespace AIAgentHub.Application.Git;

public sealed record GitFileStatusDto(string Path, string Status, bool IsStaged);

public sealed record GitStatusDto(
    bool IsGitInstalled,
    bool IsRepository,
    string? CurrentBranch,
    int Ahead,
    int Behind,
    IReadOnlyList<GitFileStatusDto> Staged,
    IReadOnlyList<GitFileStatusDto> Modified,
    IReadOnlyList<string> Untracked);

public sealed record GitBranchDto(string Name, bool IsCurrent, bool IsRemote);

public sealed record GitCommitDto(
    string Hash,
    string ShortHash,
    string Author,
    string Email,
    DateTimeOffset Date,
    string Message,
    IReadOnlyList<string>? Parents = null,
    string? Refs = null);

public sealed record GitCommitFileChangeDto(
    string Path,
    string Status,
    int Additions,
    int Deletions);

public sealed record GitCommitDetailDto(
    string Hash,
    string ShortHash,
    string Author,
    string Email,
    DateTimeOffset Date,
    string Message,
    IReadOnlyList<string> Parents,
    string? Refs,
    IReadOnlyList<GitCommitFileChangeDto> Files);

public sealed record GitCommitDiffDto(
    string Path,
    string? OriginalContent,
    string? ModifiedContent,
    bool IsBinary);

public sealed record GitOperationResult(bool Success, string? Message = null, string? Error = null);

public interface IGitService
{
    public Task<GitStatusDto> GetStatusAsync(string workspacePath, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<GitBranchDto>> GetBranchesAsync(string workspacePath, CancellationToken cancellationToken = default);
    public Task<GitOperationResult> CreateBranchAsync(string workspacePath, string branchName, bool checkout = true, CancellationToken cancellationToken = default);
    public Task<GitOperationResult> CheckoutBranchAsync(string workspacePath, string branchName, CancellationToken cancellationToken = default);
    public Task<GitOperationResult> StageAsync(string workspacePath, IReadOnlyList<string>? files = null, bool stageAll = false, CancellationToken cancellationToken = default);
    public Task<GitOperationResult> UnstageAsync(string workspacePath, IReadOnlyList<string>? files = null, bool unstageAll = false, CancellationToken cancellationToken = default);
    public Task<GitOperationResult> CommitAsync(string workspacePath, string message, bool stageAll = false, CancellationToken cancellationToken = default);
    public Task<GitOperationResult> PushAsync(string workspacePath, string? remote = null, string? branch = null, CancellationToken cancellationToken = default);
    public Task<GitOperationResult> PullAsync(string workspacePath, string? remote = null, string? branch = null, CancellationToken cancellationToken = default);
    public Task<GitOperationResult> FetchAsync(string workspacePath, string? remote = null, CancellationToken cancellationToken = default);
    public Task<GitOperationResult> StashAsync(string workspacePath, string action = "save", string? message = null, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<GitCommitDto>> GetHistoryAsync(string workspacePath, int maxCount = 50, CancellationToken cancellationToken = default);
    public Task<GitCommitDetailDto?> GetCommitDetailsAsync(string workspacePath, string commitHash, CancellationToken cancellationToken = default);
    public Task<GitCommitDiffDto?> GetCommitDiffAsync(string workspacePath, string commitHash, string filePath, CancellationToken cancellationToken = default);
    public Task<GitOperationResult> CloneAsync(string cloneUrl, string targetDirectory, CancellationToken cancellationToken = default);
}
