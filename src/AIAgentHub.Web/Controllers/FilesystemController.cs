using System.Text.Json.Serialization;
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
    private const string WorkspaceNotFoundCode = "workspace_not_found";
    private const string ForbiddenPathCode = "forbidden_path";

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
            return NotFound(new { code = WorkspaceNotFoundCode, message = $"Workspace {workspaceId} was not found." });
        }

        var tree = await _filesystemService.GetWorkspaceTreeAsync(ws.Path, ws.Settings.IgnoredFiles, cancellationToken);
        return Ok(tree);
    }

    private static bool TryResolveWorkspacePath(string workspaceRoot, string relativePath, out string fullPath, out string? error)
    {
        error = null;
        var cleanRel = (relativePath ?? string.Empty).TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, cleanRel));
        if (!fullPath.StartsWith(workspaceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !fullPath.Equals(workspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            error = "Path traversal outside workspace root is forbidden.";
            return false;
        }
        return true;
    }

    [HttpGet("file")]
    public async Task<IActionResult> GetFile([FromQuery] Guid workspaceId, [FromQuery] string path, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(workspaceId, cancellationToken);
        if (ws == null) return NotFound(new { code = WorkspaceNotFoundCode, message = $"Workspace {workspaceId} was not found." });

        if (!TryResolveWorkspacePath(ws.Path, path, out var fullPath, out var error))
        {
            return BadRequest(new { code = ForbiddenPathCode, message = error });
        }

        if (!_filesystemService.FileExists(fullPath))
        {
            return NotFound(new { code = "file_not_found", message = $"File '{path}' was not found in workspace." });
        }

        var bytes = await _filesystemService.ReadFileBytesAsync(fullPath, cancellationToken);
        var isBinary = bytes.Take(8000).Any(b => b == 0);
        var text = isBinary ? null : System.Text.Encoding.UTF8.GetString(bytes);

        return Ok(new
        {
            path,
            name = Path.GetFileName(fullPath),
            content = text,
            isBinary,
            size = bytes.Length
        });
    }

    public sealed record FileContentRequest([property: JsonRequired] Guid WorkspaceId, string Path, string? Content = null);

    [HttpPost("file")]
    public async Task<IActionResult> CreateFile([FromBody] FileContentRequest request, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(request.WorkspaceId, cancellationToken);
        if (ws == null) return NotFound(new { code = WorkspaceNotFoundCode, message = $"Workspace {request.WorkspaceId} was not found." });

        if (!TryResolveWorkspacePath(ws.Path, request.Path, out var fullPath, out var error))
        {
            return BadRequest(new { code = ForbiddenPathCode, message = error });
        }

        await _filesystemService.CreateFileAsync(fullPath, request.Content, cancellationToken);
        return Ok(new { success = true, path = request.Path });
    }

    [HttpPut("file")]
    public async Task<IActionResult> SaveFile([FromBody] FileContentRequest request, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(request.WorkspaceId, cancellationToken);
        if (ws == null) return NotFound(new { code = WorkspaceNotFoundCode, message = $"Workspace {request.WorkspaceId} was not found." });

        if (!TryResolveWorkspacePath(ws.Path, request.Path, out var fullPath, out var error))
        {
            return BadRequest(new { code = ForbiddenPathCode, message = error });
        }

        await _filesystemService.WriteFileTextAsync(fullPath, request.Content ?? string.Empty, cancellationToken);
        return Ok(new { success = true, path = request.Path });
    }

    public sealed record RenameItemRequest([property: JsonRequired] Guid WorkspaceId, string OldPath, string NewPath);

    [HttpPut("rename")]
    public async Task<IActionResult> RenameItem([FromBody] RenameItemRequest request, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(request.WorkspaceId, cancellationToken);
        if (ws == null) return NotFound(new { code = WorkspaceNotFoundCode, message = $"Workspace {request.WorkspaceId} was not found." });

        if (!TryResolveWorkspacePath(ws.Path, request.OldPath, out var oldFullPath, out var oldErr))
        {
            return BadRequest(new { code = ForbiddenPathCode, message = oldErr });
        }

        if (!TryResolveWorkspacePath(ws.Path, request.NewPath, out var newFullPath, out var newErr))
        {
            return BadRequest(new { code = ForbiddenPathCode, message = newErr });
        }

        await _filesystemService.RenameItemAsync(oldFullPath, newFullPath, cancellationToken);
        return Ok(new { success = true, oldPath = request.OldPath, newPath = request.NewPath });
    }

    public sealed record DeleteItemRequest([property: JsonRequired] Guid WorkspaceId, string Path);

    [HttpDelete("item")]
    public async Task<IActionResult> DeleteItem([FromBody] DeleteItemRequest? bodyRequest, [FromQuery] Guid? workspaceId, [FromQuery] string? path, CancellationToken cancellationToken)
    {
        var targetWsId = bodyRequest?.WorkspaceId ?? workspaceId ?? Guid.Empty;
        var targetPath = bodyRequest?.Path ?? path;

        if (targetWsId == Guid.Empty || string.IsNullOrWhiteSpace(targetPath))
        {
            return BadRequest(new { code = "invalid_request", message = "WorkspaceId and Path are required." });
        }

        var ws = await _workspaceService.GetByIdAsync(targetWsId, cancellationToken);
        if (ws == null) return NotFound(new { code = WorkspaceNotFoundCode, message = $"Workspace {targetWsId} was not found." });

        if (!TryResolveWorkspacePath(ws.Path, targetPath, out var fullPath, out var error))
        {
            return BadRequest(new { code = ForbiddenPathCode, message = error });
        }

        if (fullPath.Equals(Path.GetFullPath(ws.Path), StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { code = "cannot_delete_root", message = "Cannot delete workspace root directory." });
        }

        await _filesystemService.DeleteItemAsync(fullPath, cancellationToken);
        return Ok(new { success = true, path = targetPath });
    }

    public sealed record MoveItemRequest([property: JsonRequired] Guid WorkspaceId, string SourcePath, string DestinationDirectory);

    [HttpPost("move")]
    public async Task<IActionResult> MoveItem([FromBody] MoveItemRequest request, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(request.WorkspaceId, cancellationToken);
        if (ws == null) return NotFound(new { code = WorkspaceNotFoundCode, message = $"Workspace {request.WorkspaceId} was not found." });

        if (!TryResolveWorkspacePath(ws.Path, request.SourcePath, out var sourceFullPath, out var srcErr))
        {
            return BadRequest(new { code = ForbiddenPathCode, message = srcErr });
        }

        if (!TryResolveWorkspacePath(ws.Path, request.DestinationDirectory, out var destFullPath, out var destErr))
        {
            return BadRequest(new { code = ForbiddenPathCode, message = destErr });
        }

        await _filesystemService.MoveItemAsync(sourceFullPath, destFullPath, cancellationToken);
        return Ok(new { success = true, sourcePath = request.SourcePath, destinationDirectory = request.DestinationDirectory });
    }

    public sealed record CreateDirectoryRequest(string? Path = null, Guid? WorkspaceId = null);

    [HttpPost("mkdir")]
    public async Task<IActionResult> CreateDirectory([FromBody] CreateDirectoryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Path))
        {
            return BadRequest(new { code = "invalid_path", message = "Path cannot be empty." });
        }

        string normalized;
        if (request.WorkspaceId.HasValue && request.WorkspaceId.Value != Guid.Empty)
        {
            var ws = await _workspaceService.GetByIdAsync(request.WorkspaceId.Value, cancellationToken);
            if (ws == null) return NotFound(new { code = WorkspaceNotFoundCode, message = "Workspace not found." });

            if (!TryResolveWorkspacePath(ws.Path, request.Path, out normalized, out var err))
            {
                return BadRequest(new { code = ForbiddenPathCode, message = err });
            }
        }
        else
        {
            normalized = Path.GetFullPath(request.Path.Trim());
            if (_systemPathValidator.IsForbiddenForBrowsing(normalized, out var reason))
            {
                return BadRequest(new { code = "forbidden_system_directory", message = reason });
            }
        }

        try
        {
            await _filesystemService.CreateDirectoryAsync(normalized, cancellationToken);
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
