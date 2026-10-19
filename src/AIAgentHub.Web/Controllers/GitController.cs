using AIAgentHub.Application.FileChanges;
using AIAgentHub.Application.Git;
using AIAgentHub.Application.Workspaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentHub.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/git")]
public sealed class GitController(IGitService gitService, IWorkspaceService workspaceService, IDiffEngine? diffEngine = null) : ControllerBase
{
    private readonly IGitService _gitService = gitService;
    private readonly IWorkspaceService _workspaceService = workspaceService;
    private readonly IDiffEngine _diffEngine = diffEngine ?? new DiffEngine();

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus([FromQuery] Guid workspaceId, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(workspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        var status = await _gitService.GetStatusAsync(ws.Path, cancellationToken);
        return Ok(status);
    }

    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches([FromQuery] Guid workspaceId, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(workspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        var branches = await _gitService.GetBranchesAsync(ws.Path, cancellationToken);
        return Ok(branches);
    }

    public sealed record CreateBranchRequest(Guid WorkspaceId, string BranchName, bool Checkout = true);

    [HttpPost("branch")]
    public async Task<IActionResult> CreateBranch([FromBody] CreateBranchRequest request, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(request.WorkspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        var result = await _gitService.CreateBranchAsync(ws.Path, request.BranchName, request.Checkout, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { code = "create_branch_failed", message = result.Error ?? "Failed to create branch." });
        }

        return Ok(new { success = true, message = result.Message });
    }

    public sealed record CheckoutBranchRequest(Guid WorkspaceId, string BranchName);

    [HttpPost("checkout")]
    public async Task<IActionResult> CheckoutBranch([FromBody] CheckoutBranchRequest request, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(request.WorkspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        var result = await _gitService.CheckoutBranchAsync(ws.Path, request.BranchName, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { code = "checkout_failed", message = result.Error ?? "Failed to checkout branch." });
        }

        return Ok(new { success = true, message = result.Message });
    }

    public sealed record StageRequest(Guid WorkspaceId, IReadOnlyList<string>? Files = null, bool StageAll = false);

    [HttpPost("stage")]
    public async Task<IActionResult> Stage([FromBody] StageRequest request, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(request.WorkspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        var result = await _gitService.StageAsync(ws.Path, request.Files, request.StageAll, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { code = "stage_failed", message = result.Error ?? "Failed to stage changes." });
        }

        return Ok(new { success = true, message = result.Message });
    }

    public sealed record UnstageRequest(Guid WorkspaceId, IReadOnlyList<string>? Files = null, bool UnstageAll = false);

    [HttpPost("unstage")]
    public async Task<IActionResult> Unstage([FromBody] UnstageRequest request, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(request.WorkspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        var result = await _gitService.UnstageAsync(ws.Path, request.Files, request.UnstageAll, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { code = "unstage_failed", message = result.Error ?? "Failed to unstage changes." });
        }

        return Ok(new { success = true, message = result.Message });
    }

    public sealed record CommitRequest(Guid WorkspaceId, string Message, bool StageAll = false);

    [HttpPost("commit")]
    public async Task<IActionResult> Commit([FromBody] CommitRequest request, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(request.WorkspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        var result = await _gitService.CommitAsync(ws.Path, request.Message, request.StageAll, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { code = "commit_failed", message = result.Error ?? "Failed to create commit." });
        }

        return Ok(new { success = true, message = result.Message });
    }

    public sealed record PushRequest(Guid WorkspaceId, string? Remote = null, string? Branch = null);

    [HttpPost("push")]
    public async Task<IActionResult> Push([FromBody] PushRequest request, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(request.WorkspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        var result = await _gitService.PushAsync(ws.Path, request.Remote, request.Branch, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { code = "push_failed", message = result.Error ?? "Failed to push commits." });
        }

        return Ok(new { success = true, message = result.Message });
    }

    public sealed record PullRequest(Guid WorkspaceId, string? Remote = null, string? Branch = null);

    [HttpPost("pull")]
    public async Task<IActionResult> Pull([FromBody] PullRequest request, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(request.WorkspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        var result = await _gitService.PullAsync(ws.Path, request.Remote, request.Branch, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { code = "pull_failed", message = result.Error ?? "Failed to pull changes." });
        }

        return Ok(new { success = true, message = result.Message });
    }

    public sealed record FetchRequest(Guid WorkspaceId, string? Remote = null);

    [HttpPost("fetch")]
    public async Task<IActionResult> Fetch([FromBody] FetchRequest request, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(request.WorkspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        var result = await _gitService.FetchAsync(ws.Path, request.Remote, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { code = "fetch_failed", message = result.Error ?? "Failed to fetch remote refs." });
        }

        return Ok(new { success = true, message = result.Message });
    }

    public sealed record StashRequest(Guid WorkspaceId, string Action = "save", string? Message = null);

    [HttpPost("stash")]
    public async Task<IActionResult> Stash([FromBody] StashRequest request, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(request.WorkspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        var result = await _gitService.StashAsync(ws.Path, request.Action, request.Message, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { code = "stash_failed", message = result.Error ?? "Failed to perform stash action." });
        }

        return Ok(new { success = true, message = result.Message });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] Guid workspaceId, [FromQuery] int maxCount = 50, CancellationToken cancellationToken = default)
    {
        var ws = await _workspaceService.GetByIdAsync(workspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        var commits = await _gitService.GetHistoryAsync(ws.Path, maxCount, cancellationToken);
        return Ok(commits);
    }

    [HttpGet("commit-details")]
    public async Task<IActionResult> GetCommitDetails([FromQuery] Guid workspaceId, [FromQuery] string hash, CancellationToken cancellationToken = default)
    {
        var ws = await _workspaceService.GetByIdAsync(workspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        if (string.IsNullOrWhiteSpace(hash))
        {
            return BadRequest(new { code = "invalid_hash", message = "Commit hash cannot be empty." });
        }

        var details = await _gitService.GetCommitDetailsAsync(ws.Path, hash, cancellationToken);
        if (details == null)
        {
            return NotFound(new { code = "commit_not_found", message = "Commit not found or invalid." });
        }

        return Ok(details);
    }

    [HttpGet("commit-diff")]
    public async Task<IActionResult> GetCommitDiff([FromQuery] Guid workspaceId, [FromQuery] string hash, [FromQuery] string filePath, CancellationToken cancellationToken = default)
    {
        var ws = await _workspaceService.GetByIdAsync(workspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(filePath))
        {
            return BadRequest(new { code = "invalid_arguments", message = "Commit hash and file path cannot be empty." });
        }

        var diff = await _gitService.GetCommitDiffAsync(ws.Path, hash, filePath, cancellationToken);
        if (diff == null)
        {
            return NotFound(new { code = "diff_not_found", message = "Diff not found for specified commit and file." });
        }

        if (diff.IsBinary)
        {
            return Ok(new
            {
                id = $"{hash}:{filePath}",
                relativePath = diff.Path,
                changeType = diff.OriginalContent == null ? "Created" : (diff.ModifiedContent == null ? "Deleted" : "Modified"),
                isBinary = true,
                oldContent = (string?)null,
                newContent = (string?)null,
                additionsCount = 0,
                deletionsCount = 0,
                sideBySideLines = Array.Empty<SideBySideLine>(),
                unifiedLines = Array.Empty<DiffLine>()
            });
        }

        var textDiff = _diffEngine.CalculateTextDiff(diff.Path, diff.OriginalContent, diff.ModifiedContent);
        return Ok(new
        {
            id = $"{hash}:{filePath}",
            relativePath = diff.Path,
            changeType = diff.OriginalContent == null ? "Created" : (diff.ModifiedContent == null ? "Deleted" : "Modified"),
            isBinary = false,
            oldContent = diff.OriginalContent,
            newContent = diff.ModifiedContent,
            additionsCount = textDiff.AdditionsCount,
            deletionsCount = textDiff.DeletionsCount,
            sideBySideLines = textDiff.SideBySideLines,
            unifiedLines = textDiff.UnifiedLines
        });
    }
}
