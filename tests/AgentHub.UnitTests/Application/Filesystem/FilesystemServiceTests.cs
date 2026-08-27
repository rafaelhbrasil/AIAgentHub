using AIAgentHub.Application.Filesystem;

namespace AgentHub.UnitTests.Application.Filesystem;

public sealed class FilesystemServiceTests
{
    [Fact]
    public async Task FilesystemService_Operations_ShouldWork()
    {
        var service = new FilesystemService();
        var tempFolder = Path.Combine(Path.GetTempPath(), $"fs_test_dir_{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempFolder);

        try
        {
            var suggested = service.SuggestWorkspaceName(tempFolder);
            Assert.NotEmpty(suggested);

            var drives = await service.GetDrivesAsync();
            Assert.NotEmpty(drives);

            var browse = await service.BrowseDirectoryAsync(tempFolder);
            Assert.NotNull(browse);

            var tree = await service.GetWorkspaceTreeAsync(tempFolder);
            Assert.NotNull(tree);

            var testFile = Path.Combine(tempFolder, $"fs_test_{Guid.NewGuid():N}.txt");
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
            if (Directory.Exists(tempFolder))
            {
                try { Directory.Delete(tempFolder, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task WriteZipArchiveAsync_ShouldCreateValidZipAndExcludeIgnoredFolders()
    {
        var service = new FilesystemService();
        var tempFolder = Path.Combine(Path.GetTempPath(), $"fs_zip_test_{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempFolder);

        try
        {
            var srcDir = Path.Combine(tempFolder, "src");
            var binDir = Path.Combine(tempFolder, "bin");
            var nodeModulesDir = Path.Combine(tempFolder, "node_modules");

            _ = Directory.CreateDirectory(srcDir);
            _ = Directory.CreateDirectory(binDir);
            _ = Directory.CreateDirectory(nodeModulesDir);

            await File.WriteAllTextAsync(Path.Combine(srcDir, "App.cs"), "public class App {}");
            await File.WriteAllTextAsync(Path.Combine(binDir, "App.dll"), "binary");
            await File.WriteAllTextAsync(Path.Combine(nodeModulesDir, "package.json"), "{}");

            using var memoryStream = new MemoryStream();
            var result = await service.WriteZipArchiveAsync(tempFolder, memoryStream, ignoredPatterns: ["custom_ignore.txt"]);
            memoryStream.Position = 0;

            Assert.Equal(1, result.TotalFiles);
            Assert.Empty(result.FailedFiles);

            using var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Read);
            var entryNames = archive.Entries.Select(e => e.FullName).ToList();

            Assert.Contains("src/App.cs", entryNames);
            Assert.DoesNotContain("bin/App.dll", entryNames);
            Assert.DoesNotContain("node_modules/package.json", entryNames);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                try { Directory.Delete(tempFolder, true); } catch { }
            }
        }
    }
}
