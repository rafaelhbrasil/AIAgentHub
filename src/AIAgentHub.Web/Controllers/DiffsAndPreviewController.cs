using AIAgentHub.Application.FileChanges;
using AIAgentHub.Application.Rendering;
using AIAgentHub.Application.Workspaces;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentHub.Web.Controllers;

[ApiController]
[Route("api/v1/diffs")]
public sealed class DiffsController : ControllerBase
{
    private readonly IFileChangeService _fileChangeService;
    private readonly IWorkspaceService _workspaceService;

    public DiffsController(IFileChangeService fileChangeService, IWorkspaceService workspaceService)
    {
        _fileChangeService = fileChangeService;
        _workspaceService = workspaceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetByConversation([FromQuery] Guid conversationId, CancellationToken cancellationToken)
    {
        var changes = await _fileChangeService.GetChangesAsync(conversationId, cancellationToken);
        return Ok(changes);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDiff(Guid id, [FromQuery] Guid workspaceId, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(workspaceId, cancellationToken);
        if (ws == null)
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });

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
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken)
    {
        try
        {
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
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });

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
}

[ApiController]
[Route("api/v1/preview")]
public sealed class PreviewController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IContentRenderingManager _renderingManager;

    public PreviewController(IWorkspaceService workspaceService, IContentRenderingManager renderingManager)
    {
        _workspaceService = workspaceService;
        _renderingManager = renderingManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetPreview([FromQuery] Guid workspaceId, [FromQuery] string path, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(workspaceId, cancellationToken);
        if (ws == null)
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });

        var fullPath = Path.Combine(ws.Path, path.Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { code = "file_not_found", message = $"File '{path}' was not found in workspace." });

        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath, cancellationToken);
        var result = await _renderingManager.RenderFileAsync(fullPath, bytes, null, cancellationToken);

        return Ok(result);
    }
}
