using AIAgentHub.Application.FileChanges;
using AIAgentHub.Application.Rendering;
using AIAgentHub.Application.Workspaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentHub.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/diffs")]
public sealed class DiffsController(IFileChangeService fileChangeService, IWorkspaceService workspaceService) : ControllerBase
{
    private readonly IFileChangeService _fileChangeService = fileChangeService;
    private readonly IWorkspaceService _workspaceService = workspaceService;

    public sealed record AcceptDiffRequest(string? Content = null);

    [HttpGet]
    public async Task<IActionResult> GetByConversation([FromQuery] Guid conversationId, [FromQuery] bool pendingOnly = true, CancellationToken cancellationToken = default)
    {
        var changes = await _fileChangeService.GetChangesAsync(conversationId, cancellationToken);
        var distinctChanges = changes
            .GroupBy(c => c.RelativePath.Replace('\\', '/').TrimStart('/'), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(c => c.CreatedAtUtc).First())
            .ToList();

        if (pendingOnly)
        {
            distinctChanges = distinctChanges.Where(c => c.Status == AIAgentHub.Domain.FileChanges.ReviewStatus.Pending).ToList();
        }

        return Ok(distinctChanges);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDiff(Guid id, [FromQuery] Guid workspaceId, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(workspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        try
        {
            var diff = await _fileChangeService.GetDiffAsync(id, ws.Path, cancellationToken);
            return Ok(diff);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { code = "diff_not_found", message = $"File change {id} was not found." });
        }
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, [FromQuery] Guid? workspaceId, [FromBody] AcceptDiffRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            if (!string.IsNullOrEmpty(request?.Content) && workspaceId.HasValue)
            {
                var change = await _fileChangeService.GetByIdAsync(id, cancellationToken);
                if (change != null)
                {
                    var ws = await _workspaceService.GetByIdAsync(workspaceId.Value, cancellationToken);
                    if (ws != null)
                    {
                        var workspaceRoot = Path.GetFullPath(ws.Path);
                        var cleanRel = change.RelativePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
                        var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, cleanRel));

                        if (!fullPath.StartsWith(workspaceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        {
                            return BadRequest(new { code = "path_traversal", message = "Target path is outside workspace root." });
                        }

                        var dir = Path.GetDirectoryName(fullPath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            _ = Directory.CreateDirectory(dir);
                        }
                        await System.IO.File.WriteAllTextAsync(fullPath, request.Content, cancellationToken);
                    }
                }
            }

            await _fileChangeService.AcceptAsync(id, cancellationToken);
            return Ok(new { success = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { code = "diff_not_found", message = $"File change {id} was not found." });
        }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromQuery] Guid workspaceId, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(workspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        try
        {
            await _fileChangeService.RejectAsync(id, ws.Path, cancellationToken);
            return Ok(new { success = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { code = "diff_not_found", message = $"File change {id} was not found." });
        }
    }

    [HttpPost("accept-all")]
    public async Task<IActionResult> AcceptAll([FromQuery] Guid conversationId, CancellationToken cancellationToken)
    {
        var changes = await _fileChangeService.GetChangesAsync(conversationId, cancellationToken);
        var pendingChanges = changes
            .GroupBy(c => c.RelativePath.Replace('\\', '/').TrimStart('/'), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(c => c.CreatedAtUtc).First())
            .Where(c => c.Status == AIAgentHub.Domain.FileChanges.ReviewStatus.Pending)
            .ToList();

        foreach (var change in pendingChanges)
        {
            await _fileChangeService.AcceptAsync(change.Id, cancellationToken);
        }
        return Ok(new { success = true });
    }

    [HttpPost("reject-all")]
    public async Task<IActionResult> RejectAll([FromQuery] Guid conversationId, [FromQuery] Guid workspaceId, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(workspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        var changes = await _fileChangeService.GetChangesAsync(conversationId, cancellationToken);
        var pendingChanges = changes
            .GroupBy(c => c.RelativePath.Replace('\\', '/').TrimStart('/'), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(c => c.CreatedAtUtc).First())
            .Where(c => c.Status == AIAgentHub.Domain.FileChanges.ReviewStatus.Pending)
            .ToList();

        foreach (var change in pendingChanges)
        {
            await _fileChangeService.RejectAsync(change.Id, ws.Path, cancellationToken);
        }
        return Ok(new { success = true });
    }
}

[ApiController]
[Authorize]
[Route("api/v1/preview")]
public sealed class PreviewController(IWorkspaceService workspaceService, IContentRenderingManager renderingManager) : ControllerBase
{
    private readonly IWorkspaceService _workspaceService = workspaceService;
    private readonly IContentRenderingManager _renderingManager = renderingManager;

    [HttpGet]
    public async Task<IActionResult> GetPreview([FromQuery] Guid workspaceId, [FromQuery] string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest(new { code = "invalid_path", message = "Path parameter is required." });
        }

        var ws = await _workspaceService.GetByIdAsync(workspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        var workspaceRoot = Path.GetFullPath(ws.Path);
        var cleanRel = path.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, cleanRel));

        // Enforce strict workspace root containment
        if (!fullPath.StartsWith(workspaceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !fullPath.Equals(workspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { code = "forbidden_path", message = "Path traversal outside workspace root is forbidden." });
        }

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound(new { code = "file_not_found", message = $"File '{path}' was not found in workspace." });
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath, cancellationToken);
        var result = await _renderingManager.RenderFileAsync(fullPath, bytes, null, cancellationToken);

        return Ok(result);
    }
}
