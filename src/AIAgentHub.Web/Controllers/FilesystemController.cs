using AIAgentHub.Application.Filesystem;
using AIAgentHub.Application.Workspaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentHub.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/filesystem")]
public sealed class FilesystemController(
    IFilesystemService filesystemService,
    IWorkspaceService workspaceService,
    ISystemPathValidator systemPathValidator) : ControllerBase
{
    private readonly IFilesystemService _filesystemService = filesystemService;
    private readonly IWorkspaceService _workspaceService = workspaceService;
    private readonly ISystemPathValidator _systemPathValidator = systemPathValidator;

    [HttpGet("forbidden-paths")]
    public IActionResult GetForbiddenPaths()
    {
        return Ok(new { forbiddenPaths = _systemPathValidator.ForbiddenFolders });
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
        if (!string.IsNullOrWhiteSpace(path) && _systemPathValidator.IsForbiddenForBrowsing(path, out var reason))
        {
            return BadRequest(new { code = "forbidden_system_directory", message = reason });
        }

        var result = await _filesystemService.BrowseDirectoryAsync(path, cancellationToken);
        return Ok(result);
    }

    [HttpGet("tree")]
    public async Task<IActionResult> GetTree([FromQuery] Guid workspaceId, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(workspaceId, cancellationToken);
        if (ws == null)
        {
            return NotFound(new { code = "workspace_not_found", message = $"Workspace {workspaceId} was not found." });
        }

        var tree = await _filesystemService.GetWorkspaceTreeAsync(ws.Path, ws.Settings.IgnoredFiles, cancellationToken);
        return Ok(tree);
    }

    public sealed record CreateDirectoryRequest(string Path);

    [HttpPost("mkdir")]
    public IActionResult CreateDirectory([FromBody] CreateDirectoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Path))
        {
            return BadRequest(new { code = "invalid_path", message = "Path cannot be empty." });
        }

        var normalized = Path.GetFullPath(request.Path.Trim());
        if (_systemPathValidator.IsForbiddenForBrowsing(normalized, out var reason))
        {
            return BadRequest(new { code = "forbidden_system_directory", message = reason });
        }

        try
        {
            if (!Directory.Exists(normalized))
            {
                _ = Directory.CreateDirectory(normalized);
            }

            var dirInfo = new DirectoryInfo(normalized);
            return Ok(new
            {
                path = dirInfo.FullName,
                name = dirInfo.Name,
                isDirectory = true
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = "mkdir_failed", message = ex.Message });
        }
    }
}
