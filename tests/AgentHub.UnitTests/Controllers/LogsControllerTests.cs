using AIAgentHub.Application.Diagnostics;
using AIAgentHub.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace AgentHub.UnitTests.Controllers;

public class LogsControllerTests
{
    private readonly ILogFileService _logService = Substitute.For<ILogFileService>();
    private readonly LogsController _controller;

    public LogsControllerTests()
    {
        _controller = new LogsController(_logService);
    }

    [Fact]
    public async Task GetLogs_ReturnsOkWithFileList()
    {
        var expected = new List<LogFileSummaryDto>
        {
            new("app-2026-09-07.log", "2026-09-07", 1024, "1.0 KB", DateTime.UtcNow, true),
            new("app-2026-09-06.log", "2026-09-06", 2048, "2.0 KB", DateTime.UtcNow.AddDays(-1), false)
        };

        _logService.GetLogFilesAsync(Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _controller.GetLogs(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expected, ok.Value);
    }

    [Fact]
    public async Task GetTail_WhenFound_ReturnsOk()
    {
        var tail = new LogTailDto("app-2026-09-07.log", 2, new[] { "line 1", "line 2" });
        _logService.GetLogTailAsync("app-2026-09-07.log", 200, Arg.Any<CancellationToken>())
            .Returns(tail);

        var result = await _controller.GetTail("app-2026-09-07.log", 200, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(tail, ok.Value);
    }

    [Fact]
    public async Task GetTail_WhenNotFound_ReturnsNotFound()
    {
        _logService.GetLogTailAsync("missing.log", 200, Arg.Any<CancellationToken>())
            .Returns((LogTailDto?)null);

        var result = await _controller.GetTail("missing.log", 200, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Download_WhenFound_ReturnsFileStreamResult()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        (Stream Stream, string ContentType, string DownloadFileName)? download =
            (stream, "text/plain; charset=utf-8", "app-2026-09-07.log");

        _logService.GetLogDownloadAsync("app-2026-09-07.log", Arg.Any<CancellationToken>())
            .Returns(download);

        var result = await _controller.Download("app-2026-09-07.log", CancellationToken.None);

        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("text/plain; charset=utf-8", fileResult.ContentType);
        Assert.Equal("app-2026-09-07.log", fileResult.FileDownloadName);
    }

    [Fact]
    public async Task Download_WhenNotFound_ReturnsNotFound()
    {
        _logService.GetLogDownloadAsync("missing.log", Arg.Any<CancellationToken>())
            .Returns(((Stream Stream, string ContentType, string DownloadFileName)?)null);

        var result = await _controller.Download("missing.log", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
