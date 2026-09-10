using AIAgentHub.Application.Diagnostics;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentHub.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/logs")]
public sealed class LogsController(ILogFileService logService) : ControllerBase
{
    private readonly ILogFileService _logService = logService;

    [HttpGet]
    public async Task<IActionResult> GetLogs(CancellationToken cancellationToken)
    {
        var files = await _logService.GetLogFilesAsync(cancellationToken);
        return Ok(files);
    }

    [HttpGet("{fileName}/tail")]
    public async Task<IActionResult> GetTail(string fileName, [FromQuery] int lines = 200, CancellationToken cancellationToken = default)
    {
        var tail = await _logService.GetLogTailAsync(fileName, lines, cancellationToken);
        if (tail == null)
        {
            return NotFound(new { message = $"Log file '{fileName}' was not found or invalid." });
        }

        return Ok(tail);
    }

    [HttpGet("{fileName}/download")]
    public async Task<IActionResult> Download(string fileName, CancellationToken cancellationToken)
    {
        var result = await _logService.GetLogDownloadAsync(fileName, cancellationToken);
        if (result == null)
        {
            return NotFound(new { message = $"Log file '{fileName}' was not found or invalid." });
        }

        return File(result.Value.Stream, result.Value.ContentType, result.Value.DownloadFileName);
    }
}
