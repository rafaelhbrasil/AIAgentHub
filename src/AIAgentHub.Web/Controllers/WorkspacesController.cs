using AIAgentHub.Application.Filesystem;
using AIAgentHub.Application.Workspaces;

using Microsoft.AspNetCore.Mvc;

namespace AIAgentHub.Web.Controllers;

[ApiController]
[Route("api/v1/workspaces")]
public sealed class WorkspacesController(IWorkspaceService workspaceService, ILogger<WorkspacesController> logger) : ApiControllerBase
{
    private readonly IWorkspaceService _workspaceService = workspaceService;
    private readonly ILogger<WorkspacesController> _logger = logger;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var list = await _workspaceService.GetAllAsync(cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(id, cancellationToken);
        return ws == null ? NotFoundResponse("workspace_not_found", $"Workspace {id} was not found.") : Ok(ws);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadZip(Guid id, [FromServices] IFilesystemService filesystemService, CancellationToken cancellationToken)
    {
        var ws = await _workspaceService.GetByIdAsync(id, cancellationToken);
        if (ws == null)
        {
            return NotFoundResponse("workspace_not_found", $"Workspace {id} was not found.");
        }

        if (!filesystemService.DirectoryExists(ws.Path))
        {
            return NotFoundResponse("directory_not_found", $"Workspace root directory '{ws.Path}' does not exist on disk.");
        }

        var memoryStream = new MemoryStream();
        var zipResult = await filesystemService.WriteZipArchiveAsync(ws.Path, memoryStream, ws.Settings?.IgnoredFiles, cancellationToken);
        memoryStream.Position = 0;

        if (zipResult.FailedFiles.Count > 0)
        {
            _logger.LogWarning("Workspace {WorkspaceId} ZIP export finished with {Count} inaccessible files: {Files}", id, zipResult.FailedFiles.Count, string.Join(", ", zipResult.FailedFiles));
            Response.Headers.Append("X-Skipped-Files", System.Text.Json.JsonSerializer.Serialize(zipResult.FailedFiles));
            Response.Headers.Append("Access-Control-Expose-Headers", "X-Skipped-Files");
        }

        var rawName = string.IsNullOrWhiteSpace(ws.Name) ? "project" : ws.Name.Trim();
        var safeName = string.Join("_", rawName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "project";
        }

        return File(memoryStream, "application/zip", $"{safeName}.zip");
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _workspaceService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            return BadRequestResponse("workspace_creation_failed", ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _workspaceService.UpdateAsync(id, request, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFoundResponse("workspace_not_found", $"Workspace {id} was not found.");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _workspaceService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
