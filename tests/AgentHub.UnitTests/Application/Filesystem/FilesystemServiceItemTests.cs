using AIAgentHub.Application.Filesystem;

namespace AgentHub.UnitTests.Application.Filesystem;

public sealed class FilesystemServiceItemTests : IDisposable
{
    private readonly string _testDir;
    private readonly FilesystemService _service;

    public FilesystemServiceItemTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"FsItemTest_{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_testDir);
        _service = new FilesystemService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task CreateFileAsync_ShouldCreateFileAndDirectories()
    {
        var filePath = Path.Combine(_testDir, "sub", "test.txt");
        await _service.CreateFileAsync(filePath, "Hello World");

        Assert.True(File.Exists(filePath));
        var content = await File.ReadAllTextAsync(filePath);
        Assert.Equal("Hello World", content);
    }

    [Fact]
    public async Task CreateDirectoryAsync_ShouldCreateDirectory()
    {
        var dirPath = Path.Combine(_testDir, "nested", "folder");
        await _service.CreateDirectoryAsync(dirPath);

        Assert.True(Directory.Exists(dirPath));
    }

    [Fact]
    public async Task RenameItemAsync_File_ShouldRenameCorrectly()
    {
        var oldPath = Path.Combine(_testDir, "initial.txt");
        var newPath = Path.Combine(_testDir, "renamed.txt");
        await File.WriteAllTextAsync(oldPath, "Content");

        await _service.RenameItemAsync(oldPath, newPath);

        Assert.False(File.Exists(oldPath));
        Assert.True(File.Exists(newPath));
        Assert.Equal("Content", await File.ReadAllTextAsync(newPath));
    }

    [Fact]
    public async Task DeleteItemAsync_ShouldDeleteFileAndDirectory()
    {
        var file = Path.Combine(_testDir, "del.txt");
        await File.WriteAllTextAsync(file, "temp");
        await _service.DeleteItemAsync(file);
        Assert.False(File.Exists(file));

        var dir = Path.Combine(_testDir, "delDir");
        _ = Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "sub.txt"), "inside");
        await _service.DeleteItemAsync(dir);
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public async Task MoveItemAsync_ShouldMoveItemIntoDestinationDirectory()
    {
        var sourceFile = Path.Combine(_testDir, "fileToMove.txt");
        var destDir = Path.Combine(_testDir, "destination");
        await File.WriteAllTextAsync(sourceFile, "Moving content");

        await _service.MoveItemAsync(sourceFile, destDir);

        Assert.False(File.Exists(sourceFile));
        var expectedTarget = Path.Combine(destDir, "fileToMove.txt");
        Assert.True(File.Exists(expectedTarget));
        Assert.Equal("Moving content", await File.ReadAllTextAsync(expectedTarget));
    }
}
