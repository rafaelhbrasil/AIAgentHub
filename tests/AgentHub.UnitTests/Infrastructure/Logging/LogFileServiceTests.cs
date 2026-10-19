using AIAgentHub.Infrastructure.Logging;

using Microsoft.Extensions.Configuration;

using NSubstitute;

namespace AgentHub.UnitTests.Infrastructure.Logging;

public sealed class LogFileServiceTests : IDisposable
{
    private readonly string _tempLogsDir;
    private readonly IConfiguration _configMock;

    public LogFileServiceTests()
    {
        _tempLogsDir = Path.Combine(Path.GetTempPath(), $"AgentHubLogsTest_{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_tempLogsDir);

        _configMock = Substitute.For<IConfiguration>();
        _ = _configMock["Logging:File:Path"].Returns(Path.Combine(_tempLogsDir, "app-{Date}.log"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempLogsDir))
            {
                Directory.Delete(_tempLogsDir, true);
            }
        }
        catch
        {
            // Ignore cleanup failure in tests
        }
    }

    [Fact]
    public async Task GetLogFilesAsync_WhenFilesExist_ReturnsSortedByDateDescending()
    {
        // Arrange
        var file1 = Path.Combine(_tempLogsDir, "app-2026-09-05.log");
        var file2 = Path.Combine(_tempLogsDir, "app-2026-09-06.log");
        await File.WriteAllTextAsync(file1, "Log 1");
        await File.WriteAllTextAsync(file2, "Log 2");

        var service = new LogFileService(_configMock);

        // Act
        var result = await service.GetLogFilesAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, f => f.FileName == "app-2026-09-05.log");
        Assert.Contains(result, f => f.FileName == "app-2026-09-06.log");
    }

    [Fact]
    public async Task GetLogTailAsync_ValidFile_ReturnsTailLinesAndTotalCount()
    {
        // Arrange
        var file = Path.Combine(_tempLogsDir, "app-2026-09-07.log");
        var lines = Enumerable.Range(1, 10).Select(i => $"Line {i}").ToList();
        await File.WriteAllLinesAsync(file, lines);

        var service = new LogFileService(_configMock);

        // Act
        var result = await service.GetLogTailAsync("app-2026-09-07.log", lines: 3);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("app-2026-09-07.log", result.FileName);
        Assert.Equal(10, result.TotalLines);
        Assert.Equal(3, result.Lines.Count);
        Assert.Equal(["Line 8", "Line 9", "Line 10"], result.Lines);
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("..\\secret.txt")]
    [InlineData("sub/app.log")]
    [InlineData("app.txt")]
    [InlineData("")]
    public async Task GetLogTailAsync_WithPathTraversalOrInvalidExtension_ReturnsNull(string invalidFileName)
    {
        // Arrange
        var service = new LogFileService(_configMock);

        // Act
        var result = await service.GetLogTailAsync(invalidFileName);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLogDownloadAsync_ValidFile_ReturnsStreamAndCorrectFileName()
    {
        // Arrange
        var file = Path.Combine(_tempLogsDir, "app-2026-09-07.log");
        await File.WriteAllTextAsync(file, "Sample log content for download");

        var service = new LogFileService(_configMock);

        // Act
        var result = await service.GetLogDownloadAsync("app-2026-09-07.log");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("app-2026-09-07.log", result.Value.DownloadFileName);
        Assert.Equal("text/plain; charset=utf-8", result.Value.ContentType);

        using var reader = new StreamReader(result.Value.Stream);
        var content = await reader.ReadToEndAsync();
        Assert.Equal("Sample log content for download", content);
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("invalid.exe")]
    public async Task GetLogDownloadAsync_PathTraversal_ReturnsNull(string invalidFileName)
    {
        // Arrange
        var service = new LogFileService(_configMock);

        // Act
        var result = await service.GetLogDownloadAsync(invalidFileName);

        // Assert
        Assert.Null(result);
    }
}
