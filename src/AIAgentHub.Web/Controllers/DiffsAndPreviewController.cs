using AIAgentHub.Application.FileChanges;
using AIAgentHub.Application.Rendering;
using AIAgentHub.Application.Workspaces;

using Microsoft.AspNetCore.Mvc;

namespace AIAgentHub.Web.Controllers;

[ApiController]
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
        if (pendingOnly)
        {
            changes = changes.Where(c => c.Status == AIAgentHub.Domain.FileChanges.ReviewStatus.Pending).ToList();
        }

        var distinctChanges = changes
            .GroupBy(c => c.RelativePath.Replace('\\', '/').TrimStart('/'), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(c => c.CreatedAtUtc).First())
            .ToList();

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
                        var fullPath = Path.Combine(ws.Path, change.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                        var dir = Path.GetDirectoryName(fullPath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
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
        foreach (var change in changes)
        {
            if (change.Status == AIAgentHub.Domain.FileChanges.ReviewStatus.Pending)
            {
                await _fileChangeService.AcceptAsync(change.Id, cancellationToken);
            }
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
        foreach (var change in changes)
        {
            if (change.Status == AIAgentHub.Domain.FileChanges.ReviewStatus.Pending)
            {
                await _fileChangeService.RejectAsync(change.Id, ws.Path, cancellationToken);
            }
        }
        return Ok(new { success = true });
    }
}

[ApiController]
[Route("api/v1/preview")]
public sealed class PreviewController(IWorkspaceService workspaceService, IContentRenderingManager renderingManager) : ControllerBase
{
    private readonly IWorkspaceService _workspaceService = workspaceService;
    private readonly IContentRenderingManager _renderingManager = renderingManager;

    [HttpGet]
    public async Task<IActionResult> GetPreview([FromQuery] Guid workspaceId, [FromQuery] string path, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(workspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        var fullPath = Path.Combine(ws.Path, path.Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound(new { code = "file_not_found", message = $"File '{path}' was not found in workspace." });
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath, cancellationToken);
        var result = await _renderingManager.RenderFileAsync(fullPath, bytes, null, cancellationToken);

        return Ok(result);
    }
}
