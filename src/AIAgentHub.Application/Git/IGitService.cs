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
    string Message);

public sealed record GitOperationResult(bool Success, string? Message = null, string? Error = null);

public interface IGitService
{
    Task<GitStatusDto> GetStatusAsync(string workspacePath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitBranchDto>> GetBranchesAsync(string workspacePath, CancellationToken cancellationToken = default);
    Task<GitOperationResult> CreateBranchAsync(string workspacePath, string branchName, bool checkout = true, CancellationToken cancellationToken = default);
    Task<GitOperationResult> CheckoutBranchAsync(string workspacePath, string branchName, CancellationToken cancellationToken = default);
    Task<GitOperationResult> StageAsync(string workspacePath, IReadOnlyList<string>? files = null, bool stageAll = false, CancellationToken cancellationToken = default);
    Task<GitOperationResult> UnstageAsync(string workspacePath, IReadOnlyList<string>? files = null, bool unstageAll = false, CancellationToken cancellationToken = default);
    Task<GitOperationResult> CommitAsync(string workspacePath, string message, bool stageAll = false, CancellationToken cancellationToken = default);
    Task<GitOperationResult> PushAsync(string workspacePath, string? remote = null, string? branch = null, CancellationToken cancellationToken = default);
    Task<GitOperationResult> PullAsync(string workspacePath, string? remote = null, string? branch = null, CancellationToken cancellationToken = default);
    Task<GitOperationResult> FetchAsync(string workspacePath, string? remote = null, CancellationToken cancellationToken = default);
    Task<GitOperationResult> StashAsync(string workspacePath, string action = "save", string? message = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitCommitDto>> GetHistoryAsync(string workspacePath, int maxCount = 50, CancellationToken cancellationToken = default);
    Task<GitOperationResult> CloneAsync(string cloneUrl, string targetDirectory, CancellationToken cancellationToken = default);
}
