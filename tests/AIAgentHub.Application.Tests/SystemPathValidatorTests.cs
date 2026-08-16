using AIAgentHub.Application.Filesystem;
using System.Runtime.InteropServices;

namespace AIAgentHub.Application.Tests;

public sealed class SystemPathValidatorTests
{
    [Fact]
    public void ForbiddenFolders_Getter_ShouldReturnPopulatedList()
    {
        var validator = new SystemPathValidator();
        Assert.NotNull(validator.ForbiddenFolders);
        Assert.NotEmpty(validator.ForbiddenFolders);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsForbiddenForWorkspace_NullOrWhitespace_ShouldReturnTrue(string? path)
    {
        var validator = new SystemPathValidator();
        var forbidden = validator.IsForbiddenForWorkspace(path, out var reason);
        Assert.True(forbidden);
        Assert.NotNull(reason);
    }

    [Fact]
    public void IsForbiddenForBrowsing_RootDrives_ShouldBeAllowed()
    {
        var validator = new SystemPathValidator();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.False(validator.IsForbiddenForBrowsing(@"C:\", out _));
            Assert.False(validator.IsForbiddenForBrowsing(@"D:\", out _));
            Assert.False(validator.IsForbiddenForBrowsing(@"C:", out _));
            Assert.False(validator.IsForbiddenForBrowsing(@"D:", out _));
        }
        else
        {
            Assert.False(validator.IsForbiddenForBrowsing("/", out _));
        }
    }

    [Fact]
    public void IsForbiddenForWorkspace_RootDrives_ShouldBeForbidden()
    {
        var validator = new SystemPathValidator();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.True(validator.IsForbiddenForWorkspace(@"C:\", out var reason1));
            Assert.Contains("root drive", reason1, StringComparison.OrdinalIgnoreCase);

            Assert.True(validator.IsForbiddenForWorkspace(@"D:\", out var reason2));
            Assert.Contains("root drive", reason2, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.True(validator.IsForbiddenForWorkspace("/", out var reason));
            Assert.Contains("root drive", reason, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void IsForbiddenForBrowsingAndWorkspace_SystemFolders_ShouldBeForbidden()
    {
        var validator = new SystemPathValidator();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrEmpty(winDir))
            {
                Assert.True(validator.IsForbiddenForBrowsing(winDir, out var reason1));
                Assert.Contains("protected", reason1, StringComparison.OrdinalIgnoreCase);

                Assert.True(validator.IsForbiddenForWorkspace(winDir, out var reason2));
                Assert.Contains("protected", reason2, StringComparison.OrdinalIgnoreCase);

                var sys32 = Path.Combine(winDir, "System32");
                Assert.True(validator.IsForbiddenForBrowsing(sys32, out _));
                Assert.True(validator.IsForbiddenForWorkspace(sys32, out _));

                var sys32Relative = Path.Combine(winDir, "System32", "..", "System32");
                Assert.True(validator.IsForbiddenForBrowsing(sys32Relative, out _));
                Assert.True(validator.IsForbiddenForWorkspace(sys32Relative, out _));
            }

            Assert.True(validator.IsForbiddenForBrowsing(@"C:\$Recycle.Bin", out _));
            Assert.True(validator.IsForbiddenForBrowsing(@"D:\$Recycle.Bin", out _));
            Assert.True(validator.IsForbiddenForBrowsing(@"C:\Recovery", out _));
            Assert.True(validator.IsForbiddenForBrowsing(@"C:\System Volume Information", out _));
        }
        else
        {
            Assert.True(validator.IsForbiddenForBrowsing("/bin", out _));
            Assert.True(validator.IsForbiddenForBrowsing("/etc", out _));
            Assert.True(validator.IsForbiddenForBrowsing("/etc/nginx", out _));
        }
    }

    [Fact]
    public void IsForbiddenForBrowsingAndWorkspace_ValidUserDirectory_ShouldBeAllowed()
    {
        var validator = new SystemPathValidator();
        var tempUserPath = Path.Combine(Path.GetTempPath(), "agent-hub-test-workspace-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(tempUserPath);
            Assert.False(validator.IsForbiddenForBrowsing(tempUserPath, out var reasonBrowse));
            Assert.Null(reasonBrowse);

            Assert.False(validator.IsForbiddenForWorkspace(tempUserPath, out var reasonWs));
            Assert.Null(reasonWs);
        }
        finally
        {
            if (Directory.Exists(tempUserPath))
            {
                Directory.Delete(tempUserPath, true);
            }
        }
    }
}
