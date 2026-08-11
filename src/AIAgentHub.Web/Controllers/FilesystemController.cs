using AIAgentHub.Application.Filesystem;
using AIAgentHub.Application.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentHub.Web.Controllers;

[ApiController]
[Route("api/v1/filesystem")]
public sealed class FilesystemController : ControllerBase
{
    private readonly IFilesystemService _filesystemService;
    private readonly IWorkspaceService _workspaceService;

    public FilesystemController(IFilesystemService filesystemService, IWorkspaceService workspaceService)
    {
        _filesystemService = filesystemService;
        _workspaceService = workspaceService;
    }

    [HttpGet("drives")]
    public async Task<IActionResult> GetDrives(CancellationToken cancellationToken)
    {
        var drives = await _filesystemService.GetDrivesAsync(cancellationToken);
        return Ok(drives);
    }

    [HttpGet("browse")]
    public async Task<IActionResult> Browse([FromQuery] string? path, CancellationToken cancellationToken)
    {
        var result = await _filesystemService.BrowseDirectoryAsync(path, cancellationToken);
        return Ok(result);
    }

    [HttpGet("tree")]
    public async Task<IActionResult> GetTree([FromQuery] Guid workspaceId, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(workspaceId, cancellationToken);
        if (ws == null)
            return NotFound(new { code = "workspace_not_found", message = $"Workspace {workspaceId} was not found." });

        var tree = await _filesystemService.GetWorkspaceTreeAsync(ws.Path, ws.Settings.IgnoredFiles, cancellationToken);
        return Ok(tree);
    }
}
