using AIAgentHub.Application.Filesystem;

namespace AgentHub.UnitTests.Application.Filesystem;

public sealed class FilesystemServiceTests
{
    [Fact]
    public async Task FilesystemService_Operations_ShouldWork()
    {
        var service = new FilesystemService();
        var tempFolder = Path.GetTempPath();

        var suggested = service.SuggestWorkspaceName(tempFolder);
        Assert.NotEmpty(suggested);

        var drives = await service.GetDrivesAsync();
        Assert.NotEmpty(drives);

        var browse = await service.BrowseDirectoryAsync(tempFolder);
        Assert.NotNull(browse);

        var tree = await service.GetWorkspaceTreeAsync(tempFolder);
        Assert.NotNull(tree);

        var testFile = Path.Combine(tempFolder, $"fs_test_{Guid.NewGuid():N}.txt");
        try
        {
            await service.WriteFileTextAsync(testFile, "Hello Filesystem");
            Assert.True(service.FileExists(testFile));
            Assert.True(service.DirectoryExists(tempFolder));

            var readBackText = await service.ReadFileTextAsync(testFile);
            Assert.Equal("Hello Filesystem", readBackText);

            var readBackBytes = await service.ReadFileBytesAsync(testFile);
            Assert.NotEmpty(readBackBytes);

            await service.WriteFileBytesAsync(testFile, System.Text.Encoding.UTF8.GetBytes("Binary Data"));
            Assert.Equal("Binary Data", await service.ReadFileTextAsync(testFile));
        }
        finally
        {
            if (File.Exists(testFile))
            {
                File.Delete(testFile);
            }
        }
    }
}
